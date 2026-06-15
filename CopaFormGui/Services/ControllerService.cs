using ODT.PowerPmacComLib;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Timers;

namespace CopaFormGui.Services;

public class ControllerService : IControllerService
{
    public async Task WriteOutputValueAsync(string variableName, string value)
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(variableName)) return;

        // Try to parse the value as a double (for numeric PMAC variables)
        if (double.TryParse(value, out var doubleValue))
        {
            await WriteVariableAsync(variableName, doubleValue);
        }
        else
        {
            // Optionally handle boolean or string values for coils or other types
            // await WriteCoilAsync(variableName, value == "1" || value.ToLower() == "on");
        }
    }
    private ConnectionState _connectionState = ConnectionState.Disconnected;
    private readonly Random _random = new();
    private ISyncGpasciiCommunicationInterface? gpascii;
    private deviceProperties? deviceProp;
    private string? _lastConnectionError;

    public ConnectionState ConnectionState
    {
        get => _connectionState;
        private set
        {
            if (_connectionState != value)
            {
                _connectionState = value;
                // Log connection state transitions for diagnostics
                try { App.LogInfo($"ControllerService.ConnectionState -> {_connectionState}"); } catch { }
                var handler = ConnectionStateChanged;
                if (handler is null) return;

                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher is not null && !dispatcher.CheckAccess())
                {
                    dispatcher.BeginInvoke(new Action(() => handler(this, value)));
                }
                else
                {
                    handler(this, value);
                }
            }
        }
    }

    public bool IsConnected => ConnectionState == ConnectionState.Connected;
    public string? CurrentIpAddress => _connectedIp;
    public string? LastConnectionError => _lastConnectionError;

    public event EventHandler<ConnectionState>? ConnectionStateChanged;

    private const int ModbusPort = 22;
    private const int ConnectTimeoutMs = 30000;
    public async Task<bool> ConnectAsync(string ipAddress, string userName, string password)
    {
        try { App.LogInfo($"ConnectAsync start: {ipAddress} (suppress={_suppressConnectionStateChanges})"); } catch { }
        if (!_suppressConnectionStateChanges)
            ConnectionState = ConnectionState.Connecting;
        _lastConnectionError = null;
        _connectedIp = ipAddress;
        try
        {
            if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(userName))
            {
                if (!_suppressConnectionStateChanges)
                    ConnectionState = ConnectionState.Error;
                _lastConnectionError = "IP address and user name are required.";
                return false;
            }

            var connectTask = Task.Run(() =>
            {
                var localClient = Connect.CreateSyncGpascii(
                    CommunicationGlobals.ConnectionTypes.SSH,
                    null);

                var localDeviceProp = new deviceProperties
                {
                    IPAddress = ipAddress,
                    PortNumber = ModbusPort,
                    User = userName,
                    Password = password
                };

                var connected = localClient.ConnectGpAscii(
                    localDeviceProp.IPAddress,
                    localDeviceProp.PortNumber,
                    localDeviceProp.User,
                    localDeviceProp.Password);

                return (localClient, localDeviceProp, connected);
            });

            var timeoutTask = Task.Delay(ConnectTimeoutMs);
            if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
                throw new TimeoutException($"PMAC connect timed out after {ConnectTimeoutMs}ms");
            var connectResult = await connectTask;
            gpascii = connectResult.localClient;
            deviceProp = connectResult.localDeviceProp;

            if (!connectResult.connected || !gpascii.GpAsciiConnected)
            {
                if (!_suppressConnectionStateChanges)
                    ConnectionState = ConnectionState.Error;
                _lastConnectionError = "PLC connection was not established (ConnectGPAscii returned false or PMAC protocol not connected).";
                return false;
            }

            _savedUser = userName;
            _savedPassword = password;
            // Record whether this successful connection was user-initiated (i.e. not suppressed)
            _lastConnectionWasUserInitiated = !_suppressConnectionStateChanges;
            StopReconnect(); // cancel any in-progress reconnect loop
            try { App.LogInfo($"ConnectAsync succeeded: {ipAddress}"); } catch { }
            ConnectionState = ConnectionState.Connected;
            _heartbeatFailureCount = 0;
            StartHeartbeat();
            return true;
        }
        catch (TimeoutException)
        {
            if (!_suppressConnectionStateChanges)
                ConnectionState = ConnectionState.Error;
            _lastConnectionError = $"Connection timed out after {ConnectTimeoutMs / 1000} seconds. Check that the PLC is powered on and reachable at {ipAddress}.";
            DisconnectPMAC();
            try { App.LogInfo($"ConnectAsync timeout: {ipAddress}"); } catch { }
            return false;
        }
        catch (Exception ex)
        {
            if (!_suppressConnectionStateChanges)
                ConnectionState = ConnectionState.Error;
            _lastConnectionError = BuildConnectionErrorMessage(ex);
            App.LogException("ControllerService.ConnectAsync", ex);
            try { App.LogInfo($"ConnectAsync exception: {ex.GetType().Name} {ex.Message}"); } catch { }
            return false;
        }
    }

    // Helper to perform a silent connection attempt (no transient UI states).
    public async Task<bool> ConnectSilentlyAsync(string ipAddress, string userName, string password)
    {
        try
        {
            try { App.LogInfo($"ConnectSilentlyAsync start: {ipAddress}"); } catch { }
            _suppressConnectionStateChanges = true;
            return await ConnectAsync(ipAddress, userName, password);
        }
        finally
        {
            _suppressConnectionStateChanges = false;
            try { App.LogInfo($"ConnectSilentlyAsync end: {ipAddress}"); } catch { }
        }
    }

    private string? _connectedIp;
    private string? _savedUser;
    private string? _savedPassword;
    private CancellationTokenSource? _reconnectCts;
    // When true, ConnectAsync will avoid changing ConnectionState to Connecting/Error
    // Used for background reconnect attempts so UI stays Offline until a connection is established.
    private volatile bool _suppressConnectionStateChanges;
    private readonly SemaphoreSlim _pmacCommandLock = new(1, 1);
    private const int ReconnectIntervalMs = 5000;
    private const int CommandTimeoutMs = 2000;

    private System.Timers.Timer? _heartbeatTimer;
    private const int HeartbeatIntervalMs = 3000;
    private const int HeartbeatProbeTimeoutMs = 1500;
    private const int HeartbeatFailuresBeforeDisconnect = 2;
    private int _heartbeatFailureCount;
    private int _heartbeatInProgress;
    // True when the most recent successful connection was initiated by the user (Connect button).
    private bool _lastConnectionWasUserInitiated;

    private void StartHeartbeat()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = new System.Timers.Timer(HeartbeatIntervalMs);
        _heartbeatTimer.Elapsed += OnHeartbeat;
        _heartbeatTimer.AutoReset = true;
        _heartbeatTimer.Start();
        try { App.LogInfo("StartHeartbeat"); } catch { }
    }

    private void StopHeartbeat()
    {
        _heartbeatTimer?.Stop();
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;
        try { App.LogInfo("StopHeartbeat"); } catch { }
    }

    private void StopReconnect()
    {
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = null;
        try { App.LogInfo("StopReconnect"); } catch { }
    }

    private void StartReconnectLoop()
    {
        StopReconnect();

        try { App.LogInfo($"StartReconnectLoop: ip={_connectedIp} user={_savedUser}"); } catch { }

        var ip   = _connectedIp;
        var user = _savedUser;
        var pass = _savedPassword;

        if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(user))
        {
            ConnectionState = ConnectionState.Disconnected;
            return;
        }

        _reconnectCts = new CancellationTokenSource();
        var token = _reconnectCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try   { await Task.Delay(ReconnectIntervalMs, token); }
                catch (OperationCanceledException) { break; }

                if (token.IsCancellationRequested) break;

                try
                {
                    var success = await ConnectSilentlyAsync(ip!, user!, pass ?? string.Empty);
                    if (success)
                    {
                        // ConnectAsync already set state=Connected and started heartbeat.
                        // Clean up the CTS — the loop is done.
                        var cts = _reconnectCts;
                        _reconnectCts = null;
                        cts?.Dispose();
                        break;
                    }
                    // Connect failed silently — keep UI as Disconnected and keep trying
                }
                catch
                {
                    // Swallow exceptions during background reconnect attempts.
                    // Keep UI as Disconnected until a successful connection occurs.
                }
            }
        });
    }
    private async void OnHeartbeat(object sender, ElapsedEventArgs e)
    {
        if (Interlocked.Exchange(ref _heartbeatInProgress, 1) == 1) return;

        try
        {
            if (ConnectionState != ConnectionState.Connected) return;

            var client = gpascii;
            if (client is null)
            {
                RegisterHeartbeatFailure();
                return;
            }

            // If the underlying GPASCII client reports it's no longer connected, treat as failure.
            try
            {
                if (!client.GpAsciiConnected)
                {
                    RegisterHeartbeatFailure();
                    return;
                }
            }
            catch
            {
                // If accessing the property throws, assume disconnected.
                RegisterHeartbeatFailure();
                return;
            }

            // Additional quick TCP-level check to detect offline PMAC faster — only for user-initiated connections
            if (_lastConnectionWasUserInitiated)
            {
                try
                {
                    var ip = deviceProp?.IPAddress ?? _connectedIp;
                    if (!string.IsNullOrWhiteSpace(ip))
                    {
                        try { App.LogInfo($"Heartbeat socket check start: {ip}"); } catch { }
                        using var cts = new CancellationTokenSource(HeartbeatProbeTimeoutMs);
                        using var tcp = new System.Net.Sockets.TcpClient();
                        var connectTask = tcp.ConnectAsync(ip, ModbusPort);
                        var delayTask = Task.Delay(HeartbeatProbeTimeoutMs, cts.Token);
                        var finished = await Task.WhenAny(connectTask, delayTask);
                        if (finished != connectTask || !tcp.Connected)
                        {
                            try { App.LogInfo($"Heartbeat socket check failed: {ip}"); } catch { }
                            RegisterHeartbeatFailure();
                            return;
                        }
                        try { App.LogInfo($"Heartbeat socket check ok: {ip}"); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    try { App.LogInfo($"Heartbeat socket check exception: {ex.Message}"); } catch { }
                    RegisterHeartbeatFailure();
                    return;
                }
            }

            try
            {
                var result = await ExecuteCommandAsync(client, "VER", HeartbeatProbeTimeoutMs);
                bool alive = result is not null
                             && result.Item1 == ODT.PowerPmacComLib.Status.Ok
                             && !string.IsNullOrWhiteSpace(result.Item2);

                if (alive)
                    _heartbeatFailureCount = 0;
                else
                    RegisterHeartbeatFailure();
            }
            catch
            {
                // Any exception (SSH error, protocol error) means the link is dead
                RegisterHeartbeatFailure();
            }
        }
        finally
        {
            Interlocked.Exchange(ref _heartbeatInProgress, 0);
        }
    }

    private void RegisterHeartbeatFailure()
    {
        try { App.LogInfo($"RegisterHeartbeatFailure count={_heartbeatFailureCount + 1}"); } catch { }
        _heartbeatFailureCount++;
        if (_heartbeatFailureCount >= HeartbeatFailuresBeforeDisconnect)
        {
            MarkDisconnected();
        }
    }

    private void MarkDisconnected()
    {
        try { App.LogInfo("MarkDisconnected"); } catch { }
        StopHeartbeat();
        _heartbeatFailureCount = 0;
        _lastConnectionError = "Connection to PLC was lost unexpectedly.";
        DisconnectPMAC();
        gpascii = null;
        deviceProp = null;
        // Immediately set state to Disconnected so UI updates promptly.
        ConnectionState = ConnectionState.Disconnected;

        // Keep _connectedIp / _savedUser / _savedPassword so the reconnect loop can retry.
        StartReconnectLoop();
    }

    public void Disconnect()
    {
        StopHeartbeat();
        StopReconnect();       // cancel any background reconnect attempt
        DisconnectPMAC();
        gpascii = null;
        deviceProp = null;
        _connectedIp = null;
        _savedUser = null;
        _savedPassword = null;
        _lastConnectionError = null;
        ConnectionState = ConnectionState.Disconnected;
    }

    public void SetUserInitiatedConnection(bool enabled)
    {
        _lastConnectionWasUserInitiated = enabled;
        try { App.LogInfo($"SetUserInitiatedConnection: {enabled}"); } catch { }
    }

    private static string BuildConnectionErrorMessage(Exception ex)
    {
        if (ContainsRemotingCompatibilityError(ex))
        {
            return "PMAC library compatibility error: this ODT PMAC package requires .NET Framework APIs (Remoting) and cannot run on net8.0.";
        }

        return $"{ex.GetType().Name}: {ex.Message}";
    }

    private static bool ContainsRemotingCompatibilityError(Exception ex)
    {
        Exception? current = ex;
        while (current is not null)
        {
            var text = $"{current.GetType().FullName} {current.Message}";
            if (text.Contains("RemotingServices", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("System.Runtime.Remoting", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("TypeLoadException", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    private void DisconnectPMAC()
    {
        try
        {
            try { App.LogInfo("DisconnectPMAC"); } catch { }
            if (gpascii != null)
            {
                gpascii.DisconnectGpascii();
                Console.WriteLine("Disconnected from PMAC");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Disconnect Error: " + ex.Message);
        }
    }

    public Task<bool> ReadCoilAsync(int address)
    {
        // Stub: replace with real PLC read
        return Task.FromResult(_random.NextDouble() > 0.5);
    }

    public async Task<string?> ReadResponseAsync(string commandOrVariable)
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(commandOrVariable) || gpascii is null)
            return null;

        foreach (var command in BuildReadCommands(commandOrVariable))
        {
            try
            {
                var response = await ExecuteCommandAsync(gpascii, command, CommandTimeoutMs);
                if (response is null || response.Item1 != ODT.PowerPmacComLib.Status.Ok)
                    continue;

                var normalized = NormalizeControllerResponse(response.Item2);
                if (!string.IsNullOrWhiteSpace(normalized))
                    return normalized;
            }
            catch
            {
                // Try next syntax variant.
            }
        }

        return null;
    }

    public async Task<double?> ReadVariableAsync(string variableName)
    {
        // Always use 'echo 7 <variable>' for digital input reads to get a clean numeric response
        var response = await ReadResponseAsync($"echo 7 {variableName}");
        System.Diagnostics.Debug.WriteLine($"[PMAC] ReadVariableAsync: {variableName} => '{response}'");
        return TryParseDoubleFromResponse(response ?? string.Empty, out var value) ? value : null;
    }

    public async Task<bool> WriteVariableAsync(string variableName, double value)
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(variableName) || gpascii is null)
            return false;

        var cmd = string.Format(CultureInfo.InvariantCulture, "{0}={1}", variableName.Trim(), value);

        try
        {
            var response = await ExecuteCommandAsync(gpascii, cmd, CommandTimeoutMs);
            return response is not null && response.Item1 == ODT.PowerPmacComLib.Status.Ok;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DownloadSingleFileAsync(string localFilePath)
    {
        if (!IsConnected || gpascii is null || string.IsNullOrWhiteSpace(localFilePath))
            return false;

        if (!System.IO.File.Exists(localFilePath))
            return false;

        try
        {
            // Preferred path: use FTP + terminal gpascii single-file flow.
            var ftpGpasciiResult = await Task.Run(() => TryDownloadViaFtpAndGpascii(localFilePath));
            if (ftpGpasciiResult)
                return true;
        }
        catch
        {
            // Fall back to direct payload transfer below.
        }

        string fileContent;
        try
        {
            fileContent = await Task.Run(() => System.IO.File.ReadAllText(localFilePath));
        }
        catch
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(fileContent))
            return false;

        // Fallback: send program content in one transfer.
        var payload = fileContent.Replace("\r\n", "\n").Replace('\r', '\n');
        var timeoutMs = payload.Length * 5;
        if (timeoutMs < 5000) timeoutMs = 5000;
        if (timeoutMs > 120000) timeoutMs = 120000;

        try
        {
            var response = await ExecuteCommandAsync(gpascii, payload, timeoutMs);
            return response is not null && response.Item1 == ODT.PowerPmacComLib.Status.Ok;
        }
        catch
        {
            return false;
        }
    }

    private bool TryDownloadViaFtpAndGpascii(string localFilePath)
    {
        object? ftpClient = null;
        object? terminalClient = null;

        try
        {
            var connectType = typeof(Connect);

            var ftpEnumType = typeof(CommunicationGlobals).GetNestedType("FTPConnectionTypes");
            if (ftpEnumType is null) return false;
            var ftpMode = Enum.Parse(ftpEnumType, "FTP");

            var createFtp = connectType.GetMethod("CreateFTPClient", new[] { ftpEnumType, typeof(object) });
            if (createFtp is null) return false;
            ftpClient = createFtp.Invoke(null, new[] { ftpMode, null });
            if (ftpClient is null) return false;

            var connectionType = typeof(CommunicationGlobals).GetNestedType("ConnectionTypes");
            if (connectionType is null) return false;
            var sshMode = Enum.Parse(connectionType, "SSH");

            var createTerminal = connectType.GetMethod("CreateSyncTerminal", new[] { connectionType, typeof(object) });
            if (createTerminal is null) return false;
            terminalClient = createTerminal.Invoke(null, new[] { sshMode, null });
            if (terminalClient is null) return false;

            var ip = _connectedIp;
            var user = _savedUser;
            var pass = _savedPassword ?? string.Empty;
            if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(user))
                return false;

            var ftpConnect = ftpClient.GetType().GetMethod("ConnectFTP", new[] { typeof(string), typeof(string), typeof(string) });
            if (ftpConnect is null) return false;
            var ftpConnected = ftpConnect.Invoke(ftpClient, new object[] { ip, user, pass }) as bool?;
            if (ftpConnected != true) return false;

            var terminalConnect = terminalClient.GetType().GetMethod("ConnectTerminal", new[] { typeof(string), typeof(int), typeof(string), typeof(string) });
            if (terminalConnect is null) return false;
            var terminalConnected = terminalConnect.Invoke(terminalClient, new object[] { ip, ModbusPort, user, pass }) as bool?;
            if (terminalConnected != true) return false;

            var fileName = System.IO.Path.GetFileName(localFilePath);
            var remotePath = "/var/ftp/usrflash/Temp/" + fileName;

            var downloadFile = ftpClient.GetType().GetMethod("DownloadFile", new[] { typeof(string), typeof(string) });
            if (downloadFile is null) return false;
            downloadFile.Invoke(ftpClient, new object[] { localFilePath, remotePath });

            var sendCommand = terminalClient.GetType().GetMethod("SendCommand", new[] { typeof(string), typeof(string).MakeByRefType() });
            if (sendCommand is null) return false;

            var cmdDownload = "gpascii -i" + remotePath + " -e/var/ftp/usrflash/Project/Log/filednlderror.log";
            object?[] args = { cmdDownload, string.Empty };
            sendCommand.Invoke(terminalClient, args);
            var response = args[1]?.ToString() ?? string.Empty;

            return response.IndexOf("EOF", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            try
            {
                var disconnectTerminal = terminalClient?.GetType().GetMethod("DisconnectTerminal", Type.EmptyTypes);
                disconnectTerminal?.Invoke(terminalClient, null);
            }
            catch { }

            try
            {
                var disconnectFtp = ftpClient?.GetType().GetMethod("DisconnectFTP", Type.EmptyTypes);
                disconnectFtp?.Invoke(ftpClient, null);
            }
            catch { }
        }
    }

    public async Task<double> ReadRegisterAsync(int address)
    {
        // Map register reads to PMAC P-variables for now (e.g., 100 -> P100)
        var value = await ReadVariableAsync($"P{address}");
        return value ?? 0.0;
    }

    public Task WriteCoilAsync(int address, bool value)
    {
        // Stub: replace with real PLC write
        return Task.CompletedTask;
    }

    public async Task WriteRegisterAsync(int address, double value)
    {
        await WriteVariableAsync($"P{address}", value);
    }

    private async Task<Tuple<ODT.PowerPmacComLib.Status, string>?> ExecuteCommandAsync(
        ISyncGpasciiCommunicationInterface client,
        string command,
        int timeoutMs)
    {
        await _pmacCommandLock.WaitAsync();
        try
        {
            var commandTask = Task.Run(() =>
            {
                var status = client.GetResponse(command, out var response);
                return Tuple.Create(status, response ?? string.Empty);
            });

            var winner = await Task.WhenAny(commandTask, Task.Delay(timeoutMs));
            if (winner != commandTask)
                return null;

            return await commandTask;
        }
        finally
        {
            _pmacCommandLock.Release();
        }
    }

    private static IEnumerable<string> BuildReadCommands(string commandOrVariable)
    {
        var trimmed = commandOrVariable.Trim();
        if (trimmed.StartsWith("echo ", StringComparison.OrdinalIgnoreCase))
        {
            yield return trimmed;
            yield break;
        }

        yield return trimmed;
        yield return $"echo 7 {trimmed}";
    }

    private static string NormalizeControllerResponse(string response)
    {
        return (response ?? string.Empty)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();
    }

    private static bool TryParseDoubleFromResponse(string response, out double value)
    {
        var match = Regex.Match(response ?? string.Empty, @"[-+]?\d*\.?\d+(?:[eE][-+]?\d+)?");
        if (match.Success &&
            double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }
}

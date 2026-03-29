using ODT.PowerPmacComLib;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Timers;

namespace CopaFormGui.Services;

public class ControllerService : IControllerService
{
    public async Task WriteOutputValueAsync(int address, string value)
    {
        // TODO: Implement actual logic to send value to PMAC controller
        await Task.Delay(50); // Simulate async work
        // You might want to parse the value and call WriteCoilAsync, WriteRegisterAsync, etc.
        // Example: await WriteCoilAsync(address, value == "1" || value.ToLower() == "on");
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
        ConnectionState = ConnectionState.Connecting;
        _lastConnectionError = null;
        _connectedIp = ipAddress;
        try
        {
            if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(userName))
            {
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
                ConnectionState = ConnectionState.Error;
                _lastConnectionError = "PLC connection was not established (ConnectGPAscii returned false or PMAC protocol not connected).";
                return false;
            }

            _savedUser = userName;
            _savedPassword = password;
            StopReconnect(); // cancel any in-progress reconnect loop
            ConnectionState = ConnectionState.Connected;
            _heartbeatFailureCount = 0;
            StartHeartbeat();
            return true;
        }
        catch (TimeoutException)
        {
            ConnectionState = ConnectionState.Error;
            _lastConnectionError = $"Connection timed out after {ConnectTimeoutMs / 1000} seconds. Check that the PLC is powered on and reachable at {ipAddress}.";
            DisconnectPMAC();
            return false;
        }
        catch (Exception ex)
        {
            ConnectionState = ConnectionState.Error;
            _lastConnectionError = BuildConnectionErrorMessage(ex);
            App.LogException("ControllerService.ConnectAsync", ex);
            return false;
        }
    }

    private string? _connectedIp;
    private string? _savedUser;
    private string? _savedPassword;
    private CancellationTokenSource? _reconnectCts;
    private readonly SemaphoreSlim _pmacCommandLock = new(1, 1);
    private const int ReconnectIntervalMs = 5000;
    private const int CommandTimeoutMs = 2000;

    private System.Timers.Timer? _heartbeatTimer;
    private const int HeartbeatIntervalMs = 3000;
    private const int HeartbeatProbeTimeoutMs = 1500;
    private const int HeartbeatFailuresBeforeDisconnect = 2;
    private int _heartbeatFailureCount;
    private int _heartbeatInProgress;

    private void StartHeartbeat()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = new System.Timers.Timer(HeartbeatIntervalMs);
        _heartbeatTimer.Elapsed += OnHeartbeat;
        _heartbeatTimer.AutoReset = true;
        _heartbeatTimer.Start();
    }

    private void StopHeartbeat()
    {
        _heartbeatTimer?.Stop();
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;
    }

    private void StopReconnect()
    {
        _reconnectCts?.Cancel();
        _reconnectCts?.Dispose();
        _reconnectCts = null;
    }

    private void StartReconnectLoop()
    {
        StopReconnect();

        var ip   = _connectedIp;
        var user = _savedUser;
        var pass = _savedPassword;

        if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(user))
        {
            ConnectionState = ConnectionState.Disconnected;
            return;
        }

        ConnectionState = ConnectionState.Reconnecting;
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
                    var success = await ConnectAsync(ip!, user!, pass ?? string.Empty);
                    if (success)
                    {
                        // ConnectAsync already set state=Connected and started heartbeat.
                        // Clean up the CTS — the loop is done.
                        var cts = _reconnectCts;
                        _reconnectCts = null;
                        cts?.Dispose();
                        break;
                    }

                    // ConnectAsync failed — reset state to Reconnecting and keep trying
                    if (!token.IsCancellationRequested)
                        ConnectionState = ConnectionState.Reconnecting;
                }
                catch
                {
                    if (!token.IsCancellationRequested)
                        ConnectionState = ConnectionState.Reconnecting;
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
        _heartbeatFailureCount++;
        if (_heartbeatFailureCount >= HeartbeatFailuresBeforeDisconnect)
        {
            MarkDisconnected();
        }
    }

    private void MarkDisconnected()
    {
        StopHeartbeat();
        _heartbeatFailureCount = 0;
        _lastConnectionError = "Connection to PLC was lost unexpectedly.";
        DisconnectPMAC();
        gpascii = null;
        deviceProp = null;
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
        var response = await ReadResponseAsync(variableName);
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

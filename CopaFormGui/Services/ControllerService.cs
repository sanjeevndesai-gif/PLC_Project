using ODT.PowerPmacComLib;
using System.Timers;

namespace CopaFormGui.Services;

public class ControllerService : IControllerService
{
    private ConnectionState _connectionState = ConnectionState.Disconnected;
    private readonly Random _random = new();
    private IAsyncGpasciiCommunicationInterface? gpascii;
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
    public string? LastConnectionError => _lastConnectionError;

    public event EventHandler<ConnectionState>? ConnectionStateChanged;

    private const int ModbusPort = 22;
    private const int ConnectTimeoutMs = 15000;

    public async Task<bool> ConnectAsync(string ipAddress, string userName, string password)
    {
        ConnectionState = ConnectionState.Connecting;
        _lastConnectionError = null;
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
                var localClient = Connect.CreateAsyncGpascii(
                    CommunicationGlobals.ConnectionTypes.SSH,
                    null);

                var localDeviceProp = new deviceProperties
                {
                    IPAddress = ipAddress,
                    PortNumber = ModbusPort,
                    User = userName,
                    Password = password
                };

                var connected = localClient.ConnectGPAscii(
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

            _connectedIp = ipAddress;
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
    private const int ReconnectIntervalMs = 5000;

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

            // AsyncGetResponse only returns Ok when the command is *sent* (queued in the SSH
            // buffer), NOT when the PMAC actually responds.  If the controller goes offline
            // while the SSH socket stays open, AsyncGetResponse keeps returning Ok and the
            // heartbeat never detects the drop.
            //
            // AsyncGetResponseEx returns Task<Tuple<Status,string>> and awaits the real PMAC
            // reply, so a timeout or missing response is detected correctly.
            try
            {
                var probeTask = client.AsyncGetResponseEx("VER");
                var winner = await Task.WhenAny(probeTask, Task.Delay(HeartbeatProbeTimeoutMs));

                if (winner != probeTask)
                {
                    // Timed out — no response from PMAC
                    RegisterHeartbeatFailure();
                    return;
                }

                var result = await probeTask;
                bool alive = result.Item1 == ODT.PowerPmacComLib.Status.Ok
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

    public Task<double> ReadRegisterAsync(int address)
    {
        // Stub: replace with real PLC read
        return Task.FromResult(_random.NextDouble() * 100.0);
    }

    public Task WriteCoilAsync(int address, bool value)
    {
        // Stub: replace with real PLC write
        return Task.CompletedTask;
    }

    public Task WriteRegisterAsync(int address, double value)
    {
        // Stub: replace with real PLC write
        return Task.CompletedTask;
    }
}

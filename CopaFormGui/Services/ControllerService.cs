using System.Net.Sockets;

namespace CopaFormGui.Services;

public class ControllerService : IControllerService
{
    private ConnectionState _connectionState = ConnectionState.Disconnected;
    private readonly Random _random = new();

    public ConnectionState ConnectionState
    {
        get => _connectionState;
        private set
        {
            if (_connectionState != value)
            {
                _connectionState = value;
                ConnectionStateChanged?.Invoke(this, value);
            }
        }
    }

    public bool IsConnected => ConnectionState == ConnectionState.Connected;

    public event EventHandler<ConnectionState>? ConnectionStateChanged;

    private const int ModbusPort = 502;
    private const int ConnectTimeoutMs = 5000;

    public async Task<bool> ConnectAsync(string ipAddress, string userName, string password)
    {
        ConnectionState = ConnectionState.Connecting;
        try
        {
            if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(userName))
            {
                ConnectionState = ConnectionState.Error;
                return false;
            }

            // Real TCP connection to PLC on Modbus TCP port 502
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(ConnectTimeoutMs);
            await client.ConnectAsync(ipAddress, ModbusPort, cts.Token);

            if (!client.Connected)
            {
                ConnectionState = ConnectionState.Error;
                return false;
            }

            _connectedIp = ipAddress;
            ConnectionState = ConnectionState.Connected;
            return true;
        }
        catch (OperationCanceledException)
        {
            // Timeout
            ConnectionState = ConnectionState.Error;
            return false;
        }
        catch
        {
            ConnectionState = ConnectionState.Error;
            return false;
        }
    }

    private string? _connectedIp;

    public void Disconnect()
    {
        ConnectionState = ConnectionState.Disconnected;
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

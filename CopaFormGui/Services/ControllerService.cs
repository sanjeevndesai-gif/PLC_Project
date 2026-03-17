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

    public async Task<bool> ConnectAsync(string ipAddress, string userName, string password)
    {
        ConnectionState = ConnectionState.Connecting;
        try
        {
            // Simulate connection attempt
            await Task.Delay(1500);

            // Validate basic inputs
            if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(userName))
            {
                ConnectionState = ConnectionState.Error;
                return false;
            }

            // For demonstration purposes, simulate a successful connection
            // In production, replace with actual PLC/controller communication (e.g., Modbus TCP)
            ConnectionState = ConnectionState.Connected;
            return true;
        }
        catch
        {
            ConnectionState = ConnectionState.Error;
            return false;
        }
    }

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

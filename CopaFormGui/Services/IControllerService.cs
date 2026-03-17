namespace CopaFormGui.Services;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

public interface IControllerService
{
    ConnectionState ConnectionState { get; }
    bool IsConnected { get; }
    string? LastConnectionError { get; }
    event EventHandler<ConnectionState>? ConnectionStateChanged;

    Task<bool> ConnectAsync(string ipAddress, string userName, string password);
    void Disconnect();
    Task<bool> ReadCoilAsync(int address);
    Task<double> ReadRegisterAsync(int address);
    Task WriteCoilAsync(int address, bool value);
    Task WriteRegisterAsync(int address, double value);
}

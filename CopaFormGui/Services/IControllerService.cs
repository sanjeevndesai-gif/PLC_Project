namespace CopaFormGui.Services;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Error
}

public interface IControllerService
{
    Task WriteOutputValueAsync(string address, string value);
    ConnectionState ConnectionState { get; }
    bool IsConnected { get; }
    string? CurrentIpAddress { get; }
    string? LastConnectionError { get; }
    event EventHandler<ConnectionState>? ConnectionStateChanged;

    Task<bool> ConnectAsync(string ipAddress, string userName, string password);
    // Attempts to connect without raising transient ConnectionState changes (used by background watchers)
    Task<bool> ConnectSilentlyAsync(string ipAddress, string userName, string password);
    void Disconnect();
    // Mark whether subsequent heartbeat TCP checks should be treated as user-initiated.
    void SetUserInitiatedConnection(bool enabled);
    Task<string?> ReadResponseAsync(string commandOrVariable);
    Task<double?> ReadVariableAsync(string variableName);
    Task<bool> WriteVariableAsync(string variableName, double value);
    Task<bool> DownloadSingleFileAsync(string localFilePath);
    Task<bool> ReadCoilAsync(int address);
    Task<double> ReadRegisterAsync(int address);
    Task WriteCoilAsync(int address, bool value);
    Task WriteRegisterAsync(int address, double value);
}

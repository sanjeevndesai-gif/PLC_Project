namespace CopaFormGui.Models;

public class ConnectionSettings
{
    public string IpAddress { get; set; } = "192.168.0.200";
    public string UserName { get; set; } = "root";
    public string Password { get; set; } = "deltatau";
    public int Port { get; set; } = 502;
    public int TimeoutMs { get; set; } = 5000;
}

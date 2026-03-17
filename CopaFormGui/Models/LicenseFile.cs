namespace CopaFormGui.Models;

public class LicenseFile
{
    public string Product { get; set; } = "CopaFormGui";
    public string CustomerName { get; set; } = string.Empty;
    public string MachineId { get; set; } = string.Empty;
    public DateTime IssuedUtc { get; set; }
    public DateTime? ExpiresUtc { get; set; }
    public string Signature { get; set; } = string.Empty;
}
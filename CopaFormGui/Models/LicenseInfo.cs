namespace CopaFormGui.Models;

public class LicenseInfo
{
    public string MachineId { get; set; } = string.Empty;
    public string LicenseKey { get; set; } = string.Empty;
    public bool IsActivated { get; set; }
    public DateTime ActivatedAt { get; set; }
}

namespace CopaFormGui.Models;

public enum AlarmSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public class AlarmRecord
{
    public int AlarmId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AlarmSeverity Severity { get; set; } = AlarmSeverity.Info;
    public bool IsAcknowledged { get; set; }
    public string AcknowledgedBy { get; set; } = string.Empty;
}

namespace CopaFormGui.Models;

public class SessionRecord
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public string OperatorName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime? EndTime { get; set; }
    public bool IsActive { get; set; }
    public int TotalPunches { get; set; }
    public List<string> ProgramsRun { get; set; } = new();

    public TimeSpan Duration => (EndTime ?? DateTime.Now) - StartTime;

    public string DurationText => IsActive
        ? $"{(int)Duration.TotalHours:D2}:{Duration.Minutes:D2}:{Duration.Seconds:D2} (active)"
        : $"{(int)Duration.TotalHours:D2}:{Duration.Minutes:D2}:{Duration.Seconds:D2}";

    public string ProgramsRunText => ProgramsRun.Count > 0
        ? string.Join(", ", ProgramsRun)
        : "—";
}

namespace CopaFormGui.Models;

/// <summary>Represents a single PLC digital I/O point.</summary>
public class IOPoint
{
    public int Address { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool State { get; set; }
    public bool IsOutput { get; set; }
}

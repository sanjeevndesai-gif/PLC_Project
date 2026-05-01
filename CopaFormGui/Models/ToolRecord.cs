namespace CopaFormGui.Models;

public class ToolRecord
{
    public int ToolId { get; set; }
    public string ToolStation { get; set; } = "T1";
    public string ToolName { get; set; } = string.Empty;
    public string ToolType { get; set; } = string.Empty;
    public double Diameter { get; set; }
    public double Length { get; set; }
    public double Width { get; set; }
    public double StrokeLength { get; set; }
    public int MaxStrokes { get; set; }
    public int CurrentStrokes { get; set; }
    public string Status { get; set; } = "OK";
    public string Notes { get; set; } = string.Empty;
    public bool IsUsed { get; set; } = false; // Default to not used
}

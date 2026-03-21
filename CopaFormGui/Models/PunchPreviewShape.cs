namespace CopaFormGui.Models;

public class PunchPreviewShape
{
    public double CanvasLeft { get; set; }
    public double CanvasTop { get; set; }
    public double ShapeWidth { get; set; }
    public double ShapeHeight { get; set; }
    public bool IsRound { get; set; }
    public bool IsSquare => !IsRound;
    public string ToolLabel { get; set; } = string.Empty;
    public string FillColor { get; set; } = "#88FF7A00";
    public string StrokeColor { get; set; } = "#E65100";
    public bool IsHighlighted { get; set; }
}

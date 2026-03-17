namespace CopaFormGui.Models;

/// <summary>Represents a punch-hole position mapped to virtual canvas coordinates (800 × 150).</summary>
public class PunchPreviewPoint
{
    public double CanvasX { get; set; }
    public double CanvasY { get; set; }
    public bool IsSelected { get; set; }
}

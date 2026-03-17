namespace CopaFormGui.Models;

public class MachineSettings
{
    // Speed settings (mm/min)
    public double SpeedX { get; set; } = 1000.0;
    public double SpeedY { get; set; } = 1000.0;
    public double SpeedZ { get; set; } = 500.0;
    public double SpeedXHand { get; set; } = 200.0;
    public double SpeedYHand { get; set; } = 200.0;
    public double SpeedZHand { get; set; } = 100.0;

    // Position limits
    public double XMin { get; set; } = 0.0;
    public double XMax { get; set; } = 1000.0;
    public double YMin { get; set; } = 0.0;
    public double YMax { get; set; } = 600.0;
    public double ZMin { get; set; } = 0.0;
    public double ZMax { get; set; } = 200.0;

    // Tool lengths
    public double ToolLength1 { get; set; } = 50.0;
    public double ToolLength2 { get; set; } = 50.0;
    public double ToolLength3 { get; set; } = 50.0;

    // Home positions
    public double HomeX { get; set; } = 0.0;
    public double HomeY { get; set; } = 0.0;
    public double HomeZ { get; set; } = 100.0;

    // Safety settings
    public double SafetyHeight { get; set; } = 50.0;
    public double ClampForce { get; set; } = 100.0;
}

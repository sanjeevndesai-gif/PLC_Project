namespace CopaFormGui.Models;

public class MachineSettings
{
    // Speed settings (mm/min)
    public double SpeedX { get; set; } = 1000.0;
    public double SpeedY { get; set; } = 1000.0;
    public double SpeedXHand { get; set; } = 200.0;
    public double SpeedYHand { get; set; } = 200.0;
    public double SpeedZ { get; set; } = 1000.0;
    public double SpeedZHand { get; set; } = 200.0;

    // Position limits
    public double XMin { get; set; } = 0.0;
    public double XMax { get; set; } = 1000.0;
    public double YMin { get; set; } = 0.0;
    public double YMax { get; set; } = 600.0;
    public double ZMin { get; set; } = 0.0;
    public double ZMax { get; set; } = 500.0;

    // Tool lengths
    public double ToolLength1 { get; set; } = 50.0;
    public double ToolLength2 { get; set; } = 50.0;
    public double ToolLength3 { get; set; } = 50.0;

    // Home positions
    public double HomeX { get; set; } = 0.0;
    public double HomeY { get; set; } = 0.0;

    // Safety settings
    public double SafetyHeight { get; set; } = 50.0;
    public double ClampForce { get; set; } = 100.0;

    // Times Section
    public double SuperviseTimePunching { get; set; } = 0.0;
    public double RunningTimeBeltWorkpiece { get; set; } = 0.0;
    public double RunningTimeBeltRest { get; set; } = 0.0;
    public double WaitingTimeClosingGrippers { get; set; } = 0.0;
    public double WaitingTimeOpenGrippers { get; set; } = 0.0;
    public double WaitingTimeClosingClamping { get; set; } = 0.0;
    public double WaitingTimeOpenClamping { get; set; } = 0.0;

    // Positions and Lengths Section
    public double PartDropOffPosition { get; set; } = 0.0;
    public double GrabPositionGripper { get; set; } = 0.0;
    public double ChangeoverPositionPunching { get; set; } = 0.0;
    public double ChangeoverPositionCutting { get; set; } = 0.0;
    public double OffsetSideStop { get; set; } = 0.0;
    public double ZeroPointTool4 { get; set; } = 0.0;
    public double ChangePositionTool4 { get; set; } = 0.0;

    // Service Tab
    public double XAxisAcceleration { get; set; } = 0.0;
    public double XAxisUnblockY { get; set; } = 0.0;
    public double YAxisAcceleration { get; set; } = 0.0;
    public double YAxisUnblockXRight { get; set; } = 0.0;
    public double YAxisUnblockXLeft { get; set; } = 0.0;
    public double YAxisSideStop { get; set; } = 0.0;
    public double ZAxisAcceleration { get; set; } = 0.0;
}

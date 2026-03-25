using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Models;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IControllerService _controllerService;
    private void Log(string message)
    {
        try
        {
            System.IO.File.AppendAllText("CopaFormGui_SettingsViewModel.log", $"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch { }
    }

    [ObservableProperty] private bool _isConnected;

    // Speed Settings
    [ObservableProperty] private double _speedX = 1000.0;
    [ObservableProperty] private double _speedY = 1000.0;
    [ObservableProperty] private double _speedXHand = 200.0;
    [ObservableProperty] private double _speedYHand = 200.0;
    [ObservableProperty] private double _speedZ = 1000.0;
    [ObservableProperty] private double _speedZHand = 200.0;

    // Position Limits
    [ObservableProperty] private double _xMin = 0.0;
    [ObservableProperty] private double _xMax = 1000.0;
    [ObservableProperty] private double _yMin = 0.0;
    [ObservableProperty] private double _yMax = 600.0;
    [ObservableProperty] private double _zMin = 0.0;
    [ObservableProperty] private double _zMax = 500.0;

    // Tool Lengths
    [ObservableProperty] private double _toolLength1 = 50.0;
    [ObservableProperty] private double _toolLength2 = 50.0;
    [ObservableProperty] private double _toolLength3 = 50.0;

    // Home Positions
    [ObservableProperty] private double _homeX = 0.0;
    [ObservableProperty] private double _homeY = 0.0;

    // Safety
    [ObservableProperty] private double _safetyHeight = 50.0;
    [ObservableProperty] private double _clampForce = 100.0;

    // Times Section
    [ObservableProperty] private double _superviseTimePunching = 0.0;
    [ObservableProperty] private double _runningTimeBeltWorkpiece = 0.0;
    [ObservableProperty] private double _runningTimeBeltRest = 0.0;
    [ObservableProperty] private double _waitingTimeClosingGrippers = 0.0;
    [ObservableProperty] private double _waitingTimeOpenGrippers = 0.0;
    [ObservableProperty] private double _waitingTimeClosingClamping = 0.0;
    [ObservableProperty] private double _waitingTimeOpenClamping = 0.0;

    // Positions and Lengths Section
    [ObservableProperty] private double _partDropOffPosition = 0.0;
    [ObservableProperty] private double _grabPositionGripper = 0.0;
    [ObservableProperty] private double _changeoverPositionPunching = 0.0;
    [ObservableProperty] private double _changeoverPositionCutting = 0.0;
    [ObservableProperty] private double _offsetSideStop = 0.0;
    [ObservableProperty] private double _zeroPointTool4 = 0.0;
    [ObservableProperty] private double _changePositionTool4 = 0.0;

    // Service Tab
    [ObservableProperty] private double _xAxisAcceleration = 0.0;
    [ObservableProperty] private double _xAxisUnblockY = 0.0;
    [ObservableProperty] private double _yAxisAcceleration = 0.0;
    [ObservableProperty] private double _yAxisUnblockXRight = 0.0;
    [ObservableProperty] private double _yAxisUnblockXLeft = 0.0;
    [ObservableProperty] private double _yAxisSideStop = 0.0;
    [ObservableProperty] private double _zAxisAcceleration = 0.0;

    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private string _statusMessage = string.Empty;
    public SettingsViewModel(ISettingsService settingsService, IControllerService controllerService)
    {
        Log("SettingsViewModel constructor start");
        _settingsService = settingsService;
        _controllerService = controllerService;
        _controllerService.ConnectionStateChanged += (_, s) => IsConnected = s == ConnectionState.Connected;
        IsConnected = controllerService.IsConnected;
        try
        {
            Log("Calling LoadFromSettings");
            LoadFromSettings(_settingsService.LoadSettings());
            Log("LoadFromSettings completed");
        }
        catch (Exception ex)
        {
            Log($"Exception in constructor: {ex}");
            throw;
        }
    }

    private void LoadFromSettings(MachineSettings s)
    {
        Log($"LoadFromSettings called with: {System.Text.Json.JsonSerializer.Serialize(s)}");
        SpeedX = s.SpeedX; SpeedY = s.SpeedY; SpeedZ = s.SpeedZ;
        SpeedXHand = s.SpeedXHand; SpeedYHand = s.SpeedYHand; SpeedZHand = s.SpeedZHand;
        XMin = s.XMin; XMax = s.XMax; YMin = s.YMin; YMax = s.YMax; ZMin = s.ZMin; ZMax = s.ZMax;
        ToolLength1 = s.ToolLength1; ToolLength2 = s.ToolLength2; ToolLength3 = s.ToolLength3;
        HomeX = s.HomeX; HomeY = s.HomeY;
        SafetyHeight = s.SafetyHeight; ClampForce = s.ClampForce;
        SuperviseTimePunching = s.SuperviseTimePunching;
        RunningTimeBeltWorkpiece = s.RunningTimeBeltWorkpiece;
        RunningTimeBeltRest = s.RunningTimeBeltRest;
        WaitingTimeClosingGrippers = s.WaitingTimeClosingGrippers;
        WaitingTimeOpenGrippers = s.WaitingTimeOpenGrippers;
        WaitingTimeClosingClamping = s.WaitingTimeClosingClamping;
        WaitingTimeOpenClamping = s.WaitingTimeOpenClamping;
        PartDropOffPosition = s.PartDropOffPosition;
        GrabPositionGripper = s.GrabPositionGripper;
        ChangeoverPositionPunching = s.ChangeoverPositionPunching;
        ChangeoverPositionCutting = s.ChangeoverPositionCutting;
        OffsetSideStop = s.OffsetSideStop;
        ZeroPointTool4 = s.ZeroPointTool4;
        ChangePositionTool4 = s.ChangePositionTool4;
        XAxisAcceleration = s.XAxisAcceleration;
        XAxisUnblockY = s.XAxisUnblockY;
        YAxisAcceleration = s.YAxisAcceleration;
        YAxisUnblockXRight = s.YAxisUnblockXRight;
        YAxisUnblockXLeft = s.YAxisUnblockXLeft;
        YAxisSideStop = s.YAxisSideStop;
        ZAxisAcceleration = s.ZAxisAcceleration;
        Log("LoadFromSettings finished property assignment");
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settingsService.SaveSettings(new MachineSettings
        {
            SpeedX = SpeedX, SpeedY = SpeedY, SpeedZ = SpeedZ,
            SpeedXHand = SpeedXHand, SpeedYHand = SpeedYHand, SpeedZHand = SpeedZHand,
            XMin = XMin, XMax = XMax, YMin = YMin, YMax = YMax, ZMin = ZMin, ZMax = ZMax,
            ToolLength1 = ToolLength1, ToolLength2 = ToolLength2, ToolLength3 = ToolLength3,
            HomeX = HomeX, HomeY = HomeY,
            SafetyHeight = SafetyHeight, ClampForce = ClampForce,
            SuperviseTimePunching = SuperviseTimePunching,
            RunningTimeBeltWorkpiece = RunningTimeBeltWorkpiece,
            RunningTimeBeltRest = RunningTimeBeltRest,
            WaitingTimeClosingGrippers = WaitingTimeClosingGrippers,
            WaitingTimeOpenGrippers = WaitingTimeOpenGrippers,
            WaitingTimeClosingClamping = WaitingTimeClosingClamping,
            WaitingTimeOpenClamping = WaitingTimeOpenClamping,
            PartDropOffPosition = PartDropOffPosition,
            GrabPositionGripper = GrabPositionGripper,
            ChangeoverPositionPunching = ChangeoverPositionPunching,
            ChangeoverPositionCutting = ChangeoverPositionCutting,
            OffsetSideStop = OffsetSideStop,
            ZeroPointTool4 = ZeroPointTool4,
            ChangePositionTool4 = ChangePositionTool4,
            XAxisAcceleration = XAxisAcceleration,
            XAxisUnblockY = XAxisUnblockY,
            YAxisAcceleration = YAxisAcceleration,
            YAxisUnblockXRight = YAxisUnblockXRight,
            YAxisUnblockXLeft = YAxisUnblockXLeft,
            YAxisSideStop = YAxisSideStop,
            ZAxisAcceleration = ZAxisAcceleration
        });
        StatusMessage = "Settings saved successfully.";
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        LoadFromSettings(new MachineSettings());
        StatusMessage = "Settings reset to defaults.";
    }

    [RelayCommand]
    private void ApplySettings()
    {
        SaveSettings();
        StatusMessage = "Settings applied to controller.";
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Models;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IControllerService _controllerService;

    [ObservableProperty] private bool _isConnected;

    // Speed Settings
    [ObservableProperty] private double _speedX = 1000.0;
    [ObservableProperty] private double _speedY = 1000.0;
    [ObservableProperty] private double _speedZ = 500.0;
    [ObservableProperty] private double _speedXHand = 200.0;
    [ObservableProperty] private double _speedYHand = 200.0;
    [ObservableProperty] private double _speedZHand = 100.0;

    // Position Limits
    [ObservableProperty] private double _xMin = 0.0;
    [ObservableProperty] private double _xMax = 1000.0;
    [ObservableProperty] private double _yMin = 0.0;
    [ObservableProperty] private double _yMax = 600.0;
    [ObservableProperty] private double _zMin = 0.0;
    [ObservableProperty] private double _zMax = 200.0;

    // Tool Lengths
    [ObservableProperty] private double _toolLength1 = 50.0;
    [ObservableProperty] private double _toolLength2 = 50.0;
    [ObservableProperty] private double _toolLength3 = 50.0;

    // Home Positions
    [ObservableProperty] private double _homeX = 0.0;
    [ObservableProperty] private double _homeY = 0.0;
    [ObservableProperty] private double _homeZ = 100.0;

    // Safety
    [ObservableProperty] private double _safetyHeight = 50.0;
    [ObservableProperty] private double _clampForce = 100.0;

    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public SettingsViewModel(ISettingsService settingsService, IControllerService controllerService)
    {
        _settingsService = settingsService;
        _controllerService = controllerService;
        _controllerService.ConnectionStateChanged += (_, s) => IsConnected = s == ConnectionState.Connected;
        IsConnected = controllerService.IsConnected;
        LoadFromSettings(_settingsService.LoadSettings());
    }

    private void LoadFromSettings(MachineSettings s)
    {
        SpeedX = s.SpeedX; SpeedY = s.SpeedY; SpeedZ = s.SpeedZ;
        SpeedXHand = s.SpeedXHand; SpeedYHand = s.SpeedYHand; SpeedZHand = s.SpeedZHand;
        XMin = s.XMin; XMax = s.XMax;
        YMin = s.YMin; YMax = s.YMax;
        ZMin = s.ZMin; ZMax = s.ZMax;
        ToolLength1 = s.ToolLength1; ToolLength2 = s.ToolLength2; ToolLength3 = s.ToolLength3;
        HomeX = s.HomeX; HomeY = s.HomeY; HomeZ = s.HomeZ;
        SafetyHeight = s.SafetyHeight; ClampForce = s.ClampForce;
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
            HomeX = HomeX, HomeY = HomeY, HomeZ = HomeZ,
            SafetyHeight = SafetyHeight, ClampForce = ClampForce
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

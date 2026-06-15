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
    [ObservableProperty] private double _toolLength4 = 50.0;

    // Tool Offsets
    [ObservableProperty] private double _t1OffsetPos = 0.0;
    [ObservableProperty] private double _t2OffsetPos = 0.0;
    [ObservableProperty] private double _t3OffsetPos = 0.0;
    [ObservableProperty] private double _t4OffsetPos = 0.0;
    // Tool X Offsets
    [ObservableProperty] private double _t1OffsetPosX = 0.0;
    [ObservableProperty] private double _t2OffsetPosX = 0.0;
    [ObservableProperty] private double _t3OffsetPosX = 0.0;
    [ObservableProperty] private double _t4OffsetPosX = 0.0;

    // Home Positions
    [ObservableProperty] private double _homeX = 0.0;
    [ObservableProperty] private double _homeY = 0.0;
    // Stored home position values captured from motors
    [ObservableProperty] private double _homeXPos = 0.0;
    [ObservableProperty] private double _homeYPos = 0.0;

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
            // Subscribe to the controller's ConnectionStateChanged using the existing handler
            _controllerService.ConnectionStateChanged += OnConnectionStateChanged;
            IsConnected = controllerService.IsConnected;

            // Auto-save settings when properties change (except UI-only properties)
            this.PropertyChanged += async (s, e) =>
            {
                try
                {
                    if (string.IsNullOrEmpty(e?.PropertyName)) return;
                    if (e.PropertyName == nameof(StatusMessage) || e.PropertyName == nameof(IsConnected) || e.PropertyName == nameof(SelectedTabIndex))
                        return;

                    // Persist settings whenever a setting property changes
                    SaveSettings();
                }
                catch { }
            };
        try
        {
            // If already connected at startup, push settings once
            if (IsConnected)
            {
                _ = ApplySettings();
            }
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
        ToolLength1 = s.ToolLength1; ToolLength2 = s.ToolLength2; ToolLength3 = s.ToolLength3; ToolLength4 = s.ToolLength4;
        T1OffsetPos = s.T1OffsetPos; T2OffsetPos = s.T2OffsetPos; T3OffsetPos = s.T3OffsetPos; T4OffsetPos = s.T4OffsetPos;
        T1OffsetPosX = s.T1OffsetPosX; T2OffsetPosX = s.T2OffsetPosX; T3OffsetPosX = s.T3OffsetPosX; T4OffsetPosX = s.T4OffsetPosX;
        HomeX = s.HomeX; HomeY = s.HomeY;
        HomeXPos = s.HOMEX_POS; HomeYPos = s.HOMEY_POS;
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

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        IsConnected = state == ConnectionState.Connected;
        if (IsConnected)
        {
            // push current settings to controller once after connection/reconnect
            _ = ApplySettings();
        }
    }
    [RelayCommand]
    private void SaveSettings()
    {
        _settingsService.SaveSettings(new MachineSettings
        {
            SpeedX = SpeedX, SpeedY = SpeedY, SpeedZ = SpeedZ,
            SpeedXHand = SpeedXHand, SpeedYHand = SpeedYHand, SpeedZHand = SpeedZHand,
            XMin = XMin, XMax = XMax, YMin = YMin, YMax = YMax, ZMin = ZMin, ZMax = ZMax,
            ToolLength1 = ToolLength1, ToolLength2 = ToolLength2, ToolLength3 = ToolLength3, ToolLength4 = ToolLength4,
            T1OffsetPos = T1OffsetPos, T2OffsetPos = T2OffsetPos, T3OffsetPos = T3OffsetPos, T4OffsetPos = T4OffsetPos,
                T1OffsetPosX = T1OffsetPosX, T2OffsetPosX = T2OffsetPosX, T3OffsetPosX = T3OffsetPosX, T4OffsetPosX = T4OffsetPosX,
            HomeX = HomeX, HomeY = HomeY,
            HOMEX_POS = HomeXPos, HOMEY_POS = HomeYPos,
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
    private async Task SetHomeXPos()
    {
        if (!_controllerService.IsConnected)
        {
            StatusMessage = "Controller not connected.";
            return;
        }

        var val = await _controllerService.ReadVariableAsync("Motor[1].Actpos");
        if (val is null)
        {
            StatusMessage = "Failed to read Motor[1].Actpos.";
            return;
        }

        HomeXPos = val.Value;
        SaveSettings();
        // Send new home position and trigger home write, then wait and verify
        await _controllerService.WriteVariableAsync("HOMEX_POS", HomeXPos);
        await _controllerService.WriteVariableAsync("HOMEX", 1);
        StatusMessage = $"HOMEX_POS sent ({HomeXPos:0.###}), awaiting controller response...";

        // Wait 3 seconds to allow controller to process
        await Task.Delay(3000);

        // Read back stored value to confirm
        var verify = await _controllerService.ReadVariableAsync("HOMEX_POS");
        if (verify is not null)
        {
            HomeXPos = verify.Value;
            SaveSettings();
            StatusMessage = $"HOMEX_POS verified as {HomeXPos:0.###}";
        }
        else
        {
            StatusMessage = "HOMEX_POS write sent but verification failed.";
        }
    }

    [RelayCommand]
    private async Task SetHomeYPos()
    {
        if (!_controllerService.IsConnected)
        {
            StatusMessage = "Controller not connected.";
            return;
        }

        var val = await _controllerService.ReadVariableAsync("Motor[2].Actpos");
        if (val is null)
        {
            StatusMessage = "Failed to read Motor[2].Actpos.";
            return;
        }

        HomeYPos = val.Value;
        SaveSettings();
        // Send new home position and trigger home write, then wait and verify
        await _controllerService.WriteVariableAsync("HOMEY_POS", HomeYPos);
        await _controllerService.WriteVariableAsync("HOMEY", 1);
        StatusMessage = $"HOMEY_POS sent ({HomeYPos:0.###}), awaiting controller response...";

        // Wait 3 seconds to allow controller to process
        await Task.Delay(3000);

        // Read back stored value to confirm
        var verify = await _controllerService.ReadVariableAsync("HOMEY_POS");
        if (verify is not null)
        {
            HomeYPos = verify.Value;
            SaveSettings();
            StatusMessage = $"HOMEY_POS verified as {HomeYPos:0.###}";
        }
        else
        {
            StatusMessage = "HOMEY_POS write sent but verification failed.";
        }
    }

    [RelayCommand]
    private async Task ApplySettings()
    {
        SaveSettings();
        if (IsConnected)
        {
            await _controllerService.WriteVariableAsync("T1_pos", T1OffsetPos);
            await _controllerService.WriteVariableAsync("T2_pos", T2OffsetPos);
            await _controllerService.WriteVariableAsync("T3_pos", T3OffsetPos);
            await _controllerService.WriteVariableAsync("T4_pos", T4OffsetPos);
            // X offsets (PMAC variables)
            await _controllerService.WriteVariableAsync("T1_POSX", T1OffsetPosX);
            await _controllerService.WriteVariableAsync("T2_POSX", T2OffsetPosX);
            await _controllerService.WriteVariableAsync("T3_POSX", T3OffsetPosX);
            await _controllerService.WriteVariableAsync("T4_POSX", T4OffsetPosX);
            // Ensure stored home positions are sent on every reconnect
            await _controllerService.WriteVariableAsync("HOMEX_POS", HomeXPos);
            await _controllerService.WriteVariableAsync("HOMEY_POS", HomeYPos);
            await _controllerService.WriteVariableAsync("JOGSPEED", SpeedX);
            await _controllerService.WriteVariableAsync("HOME_FEEDRATE", SpeedY);
            await _controllerService.WriteVariableAsync("AUTO_FEEDRATE", SpeedXHand);
            await _controllerService.WriteVariableAsync("X_MINPOS", XMin);
            await _controllerService.WriteVariableAsync("X_MAXPOS", XMax);
            await _controllerService.WriteVariableAsync("Y_MINPOS", YMin);
            await _controllerService.WriteVariableAsync("Y_MAXPOS", YMax);
        }
        StatusMessage = "Settings applied to controller.";
    }
}

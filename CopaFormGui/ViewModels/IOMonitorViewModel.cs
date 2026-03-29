using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Models;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;

public partial class IOMonitorViewModel : ObservableObject
{
    private readonly IControllerService _controllerService;
    private System.Timers.Timer? _refreshTimer;

    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isAutoRefresh = true;
    [ObservableProperty] private string _statusMessage = "Monitoring I/O points";

    [ObservableProperty]
    private ObservableCollection<IOPoint> _inputs = new();

    [ObservableProperty]
    private ObservableCollection<IOPoint> _outputs = new();

    public IOMonitorViewModel(IControllerService controllerService)
    {
        _controllerService = controllerService;
        _controllerService.ConnectionStateChanged += OnConnectionStateChanged;
        IsConnected = controllerService.IsConnected;
        LoadPoints();

        // Subscribe to OutputValueChanged event
        CopaFormGui.Models.IOPoint.OutputValueChanged += OnOutputValueChanged;

        // Fetch initial I/O states immediately
        _ = RefreshStatesAsync();
    }

    private async void OnOutputValueChanged(CopaFormGui.Models.IOPoint point, string value)
    {
        if (point.IsOutput && IsConnected)
        {
            // Send value ("1" or "0") to PMAC controller
            await _controllerService.WriteOutputValueAsync(point.Name, value);
            StatusMessage = $"Output {point.Name} set to {(value == "1" ? "ON" : "OFF")}";
        }
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        IsConnected = state == ConnectionState.Connected;
        if (IsConnected)
        {
            StartAutoRefresh();
            // Fetch I/O states immediately after connecting
            _ = RefreshStatesAsync();
        }
        else
        {
            StopAutoRefresh();
        }
    }

    private void LoadPoints()
    {
        Inputs = new ObservableCollection<IOPoint>
        {
            new() { Address = 0, Name = "X_NLIMIT_UI", Description = "X- Limit", IsOutput = false, State = false },
            new() { Address = 1, Name = "X_PLIMIT_UI", Description = "X+ Limit", IsOutput = false, State = false },
            new() { Address = 2, Name = "HYDRAULIC_DOWN_SENS_UI", Description = "Hydraulic Down Sensor", IsOutput = false, State = false },
            new() { Address = 3, Name = "HYDRAULIC_UP_SENS_UI", Description = "Hydraulic Up Sensor", IsOutput = false, State = false },
            new() { Address = 4, Name = "DOOR_LS_UI", Description = "Door Limit Switch", IsOutput = false, State = false },
            new() { Address = 5, Name = "BUSBAR_CLAMP_RSW_UI", Description = "Busbar Clamp RSW", IsOutput = false, State = false },
            new() { Address = 6, Name = "FOOT_SW_UI", Description = "Foot Switch", IsOutput = false, State = false },
            new() { Address = 7, Name = "OIL_LEVEL_LOW_SENS_UI", Description = "Oil Level Low", IsOutput = false, State = false },
            new() { Address = 8, Name = "OIL_LEVEL_HIGH_SENS_UI", Description = "Oil Level High", IsOutput = false, State = false },
            new() { Address = 9, Name = "CONTROL_ON_PB_UI", Description = "Control On PB", IsOutput = false, State = false },
            new() { Address = 10, Name = "CONTROLL_OFF_PB_UI", Description = "Control Off PB", IsOutput = false, State = false },
            new() { Address = 11, Name = "MANUAL_PB_UI", Description = "Manual PB", IsOutput = false, State = false },
            new() { Address = 12, Name = "AUTO_PB_UI", Description = "Auto PB", IsOutput = false, State = false },
            new() { Address = 13, Name = "HYDRAULIC_ON_OFF_SW_UI", Description = "Hydraulic On/Off Switch", IsOutput = false, State = false },
            new() { Address = 14, Name = "Emergency_PB_UI", Description = "Emergency PB", IsOutput = false, State = false },
            new() { Address = 15, Name = "Y_NLIMIT_UI", Description = "Y- Limit", IsOutput = false, State = false },
            new() { Address = 16, Name = "Y_PLIMIT_UI", Description = "Y+ Limit", IsOutput = false, State = false },
            new() { Address = 17, Name = "BUSBAR_PRESENT_SENS_UI", Description = "Busbar Present Sensor", IsOutput = false, State = false },
        };

        Outputs = new ObservableCollection<IOPoint>
        {
            new() { Address = 0, Name = "HYDRAULIC_MOTOR_CMD_STATUS", Description = "Hydraulic Motor Cmd Status", IsOutput = true, State = false },
            new() { Address = 1, Name = "SERVO_ON_CMD_STATUS", Description = "Servo On Cmd Status", IsOutput = true, State = false },
            new() { Address = 2, Name = "OIL_COOLING_FAN_CMD_STATUS", Description = "Oil Cooling Fan Cmd Status", IsOutput = true, State = false },
            new() { Address = 3, Name = "CONVEYOR_CMD_STATUS", Description = "Conveyor Cmd Status", IsOutput = true, State = false },
            new() { Address = 4, Name = "BUSBAR_CLAMP_C1_STATUS", Description = "Busbar Clamp C1 Status", IsOutput = true, State = false },
            new() { Address = 5, Name = "BUSBAR_CLAMP_C2_STATUS", Description = "Busbar Clamp C2 Status", IsOutput = true, State = false },
            new() { Address = 6, Name = "HYDRAULIC_DOWN_CMD_STATUS", Description = "Hydraulic Down Cmd Status", IsOutput = true, State = false },
            new() { Address = 7, Name = "HYDRAULIC_UP_CMD_STATUS", Description = "Hydraulic Up Cmd Status", IsOutput = true, State = false },
            new() { Address = 8, Name = "BUSBAR_HOLD_CYL_STATUS", Description = "Busbar Hold Cylinder Status", IsOutput = true, State = false },
            new() { Address = 9, Name = "CONTROL_ON_LAMP_STATUS", Description = "Control On Lamp Status", IsOutput = true, State = false },
            new() { Address = 10, Name = "MANUAL_ON_LAMP_STATUS", Description = "Manual On Lamp Status", IsOutput = true, State = false },
            new() { Address = 11, Name = "AUTO_ON_LAMP_STATUS", Description = "Auto On Lamp Status", IsOutput = true, State = false },
        };
    }

    private void StartAutoRefresh()
    {
        _refreshTimer = new System.Timers.Timer(200); // 200ms for near real-time updates
        _refreshTimer.Elapsed += async (_, _) =>
        {
            try
            {
                await RefreshStatesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IOMonitor] Timer exception: {ex}");
            }
        };
        _refreshTimer.Start();
    }

    private void StopAutoRefresh()
    {
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
        _refreshTimer = null;
    }

    private async Task RefreshStatesAsync()
    {
        if (!IsConnected) return;
        foreach (var pt in Inputs)
        {
            var value = await _controllerService.ReadVariableAsync(pt.Name);
            pt.State = value.HasValue && value.Value != 0;
        }
        foreach (var pt in Outputs)
        {
            var value = await _controllerService.ReadVariableAsync(pt.Name);
            pt.State = value.HasValue && value.Value != 0;
        }
        StatusMessage = $"Last refresh: {DateTime.Now:HH:mm:ss}";
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await RefreshStatesAsync();
        StatusMessage = $"Refreshed at {DateTime.Now:HH:mm:ss}";
    }

    [RelayCommand]
    private async Task ToggleOutput(IOPoint? point)
    {
        if (point is null || !point.IsOutput) return;
        point.State = !point.State;
        // OutputValueChanged event will handle sending to PMAC
        // Optionally, you can still call WriteCoilAsync for redundancy:
        // await _controllerService.WriteCoilAsync(point.Address, point.State);
        StatusMessage = $"Output {point.Name} set to {(point.State ? "ON" : "OFF")}";
    }
}

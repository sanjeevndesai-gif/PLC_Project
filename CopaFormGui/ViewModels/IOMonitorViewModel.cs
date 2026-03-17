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
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        IsConnected = state == ConnectionState.Connected;
        if (IsConnected)
            StartAutoRefresh();
        else
            StopAutoRefresh();
    }

    private void LoadPoints()
    {
        Inputs = new ObservableCollection<IOPoint>
        {
            new() { Address =  0, Name = "X1.00", Description = "E-Stop Input",           IsOutput = false, State = false },
            new() { Address =  1, Name = "X1.01", Description = "Machine Ready",           IsOutput = false, State = true  },
            new() { Address =  2, Name = "X1.02", Description = "X Home Switch",           IsOutput = false, State = false },
            new() { Address =  3, Name = "X1.03", Description = "Y Home Switch",           IsOutput = false, State = false },
            new() { Address =  4, Name = "X1.04", Description = "Z Home Switch",           IsOutput = false, State = false },
            new() { Address =  5, Name = "X1.05", Description = "X+ Limit",                IsOutput = false, State = false },
            new() { Address =  6, Name = "X1.06", Description = "X- Limit",                IsOutput = false, State = false },
            new() { Address =  7, Name = "X1.07", Description = "Y+ Limit",                IsOutput = false, State = false },
            new() { Address =  8, Name = "X2.00", Description = "Y- Limit",                IsOutput = false, State = false },
            new() { Address =  9, Name = "X2.01", Description = "Z+ Limit",                IsOutput = false, State = false },
            new() { Address = 10, Name = "X2.02", Description = "Z- Limit",                IsOutput = false, State = false },
            new() { Address = 11, Name = "X2.03", Description = "Punch Up Sensor",         IsOutput = false, State = true  },
            new() { Address = 12, Name = "X2.04", Description = "Punch Down Sensor",       IsOutput = false, State = false },
            new() { Address = 13, Name = "X2.05", Description = "Clamp Open Sensor",       IsOutput = false, State = true  },
            new() { Address = 14, Name = "X2.06", Description = "Clamp Close Sensor",      IsOutput = false, State = false },
            new() { Address = 15, Name = "X2.07", Description = "Sheet Present Sensor",    IsOutput = false, State = false },
        };

        Outputs = new ObservableCollection<IOPoint>
        {
            new() { Address = 100, Name = "Y1.00", Description = "X Servo Enable",         IsOutput = true, State = false },
            new() { Address = 101, Name = "Y1.01", Description = "Y Servo Enable",         IsOutput = true, State = false },
            new() { Address = 102, Name = "Y1.02", Description = "Z Servo Enable",         IsOutput = true, State = false },
            new() { Address = 103, Name = "Y1.03", Description = "Punch Solenoid",         IsOutput = true, State = false },
            new() { Address = 104, Name = "Y1.04", Description = "Clamp Solenoid",         IsOutput = true, State = false },
            new() { Address = 105, Name = "Y1.05", Description = "Machine Ready Lamp",     IsOutput = true, State = true  },
            new() { Address = 106, Name = "Y1.06", Description = "Alarm Lamp",             IsOutput = true, State = false },
            new() { Address = 107, Name = "Y1.07", Description = "Cycle Running Lamp",     IsOutput = true, State = false },
            new() { Address = 108, Name = "Y2.00", Description = "Auto Mode Lamp",         IsOutput = true, State = false },
            new() { Address = 109, Name = "Y2.01", Description = "Air Solenoid 1",         IsOutput = true, State = false },
            new() { Address = 110, Name = "Y2.02", Description = "Air Solenoid 2",         IsOutput = true, State = false },
            new() { Address = 111, Name = "Y2.03", Description = "Buzzer",                 IsOutput = true, State = false },
        };
    }

    private void StartAutoRefresh()
    {
        _refreshTimer = new System.Timers.Timer(1000);
        _refreshTimer.Elapsed += async (_, _) => await RefreshStatesAsync();
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
            pt.State = await _controllerService.ReadCoilAsync(pt.Address);
        foreach (var pt in Outputs)
            pt.State = await _controllerService.ReadCoilAsync(pt.Address);

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
        await _controllerService.WriteCoilAsync(point.Address, point.State);
        StatusMessage = $"Output {point.Name} set to {(point.State ? "ON" : "OFF")}";
    }
}

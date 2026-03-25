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
            new() { Address =  5, Name = "X1.05", Description = "X+ Limit",                IsOutput = false, State = false },
            new() { Address =  6, Name = "X1.06", Description = "X- Limit",                IsOutput = false, State = false },
            new() { Address =  7, Name = "X1.07", Description = "Y+ Limit",                IsOutput = false, State = false },
            new() { Address =  8, Name = "X2.00", Description = "Y- Limit",                IsOutput = false, State = false },
            new() { Address = 11, Name = "X2.03", Description = "Punch Up Sensor",         IsOutput = false, State = true  },
            new() { Address = 12, Name = "X2.04", Description = "Punch Down Sensor",       IsOutput = false, State = false },
            new() { Address = 15, Name = "X2.07", Description = "Sheet Present Sensor",    IsOutput = false, State = false },

            // New sensors added below
            new() { Address = 16, Name = "X3.00", Description = "Door Limit Switch",       IsOutput = false, State = false },
            new() { Address = 17, Name = "X3.01", Description = "Foot Switch",            IsOutput = false, State = false },
            new() { Address = 18, Name = "X3.02", Description = "Oil Level Low",          IsOutput = false, State = false },
            new() { Address = 19, Name = "X3.03", Description = "Oil Level High",         IsOutput = false, State = false },
            new() { Address = 20, Name = "X3.04", Description = "Control On",             IsOutput = false, State = false },
            new() { Address = 21, Name = "X3.05", Description = "Control Off",            IsOutput = false, State = false },
            new() { Address = 22, Name = "X3.06", Description = "Manual",                 IsOutput = false, State = false },
            new() { Address = 23, Name = "X3.07", Description = "Auto",                   IsOutput = false, State = false },
            new() { Address = 24, Name = "X3.10", Description = "Hydrolic On/Off",        IsOutput = false, State = false },
        };

        Outputs = new ObservableCollection<IOPoint>
        {
            new() { Address = 100, Name = "Y1.00", Description = "X Servo Enable",         IsOutput = true, State = false },
            new() { Address = 101, Name = "Y1.01", Description = "Y Servo Enable",         IsOutput = true, State = false },
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

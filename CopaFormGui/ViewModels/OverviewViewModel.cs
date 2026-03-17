using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;

public partial class OverviewViewModel : ObservableObject
{
    private readonly IControllerService _controllerService;
    private System.Timers.Timer? _pollTimer;

    [ObservableProperty] private bool _isConnected;

    // Axis positions
    [ObservableProperty] private double _posX;
    [ObservableProperty] private double _posY;

    // Machine state
    [ObservableProperty] private string _machineMode = "Manual";
    [ObservableProperty] private bool _isMachineRunning;
    [ObservableProperty] private bool _isEStop;
    [ObservableProperty] private bool _isAlarmActive;
    [ObservableProperty] private string _activeAlarmText = "No active alarms";

    // Current job
    [ObservableProperty] private string _currentJob = "—";
    [ObservableProperty] private int _completedStrokes;
    [ObservableProperty] private int _totalStrokes = 1;
    [ObservableProperty] private double _progressPct;

    // Spindle / punch
    [ObservableProperty] private double _punchForce;
    [ObservableProperty] private double _airPressure = 6.2;

    // Controller info
    [ObservableProperty] private string _controllerIp = "192.168.0.200";
    [ObservableProperty] private string _firmwareVersion = "v3.1.4";
    [ObservableProperty] private string _lastConnected = "—";
    [ObservableProperty] private string _statusMessage = "Ready";

    // Quick-status tiles
    public string ModeColor => IsMachineRunning ? "#28A745" : "#1565C0";
    public string EStopColor => IsEStop ? "#DC3545" : "#BDBDBD";
    public string AlarmColor => IsAlarmActive ? "#FFC107" : "#BDBDBD";

    public OverviewViewModel(IControllerService controllerService)
    {
        _controllerService = controllerService;
        _controllerService.ConnectionStateChanged += OnConnectionStateChanged;
        IsConnected = controllerService.IsConnected;
        if (IsConnected) StartPolling();
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        IsConnected = state == ConnectionState.Connected;
        LastConnected = IsConnected ? DateTime.Now.ToString("HH:mm:ss  dd/MM/yyyy") : LastConnected;
        if (IsConnected) StartPolling(); else StopPolling();
        OnPropertyChanged(nameof(ModeColor));
    }

    private void StartPolling()
    {
        _pollTimer = new System.Timers.Timer(800);
        _pollTimer.Elapsed += async (_, _) => await PollAsync();
        _pollTimer.Start();
    }

    private void StopPolling()
    {
        _pollTimer?.Stop();
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    private async Task PollAsync()
    {
        if (!IsConnected) return;
        PosX = await _controllerService.ReadRegisterAsync(100);
        PosY = await _controllerService.ReadRegisterAsync(101);
        PunchForce = await _controllerService.ReadRegisterAsync(110);
        AirPressure = 5.8 + (PunchForce % 1.0);
        ProgressPct = TotalStrokes > 0 ? Math.Min(100.0, CompletedStrokes * 100.0 / TotalStrokes) : 0;
    }

    [RelayCommand]
    private void EmergencyStop()
    {
        IsEStop = true;
        IsMachineRunning = false;
        StatusMessage = "⚠ EMERGENCY STOP ACTIVATED";
        OnPropertyChanged(nameof(EStopColor));
    }

    [RelayCommand]
    private void ResetAlarms()
    {
        IsAlarmActive = false;
        IsEStop = false;
        ActiveAlarmText = "No active alarms";
        StatusMessage = "Alarms cleared.";
        OnPropertyChanged(nameof(AlarmColor));
        OnPropertyChanged(nameof(EStopColor));
    }
}

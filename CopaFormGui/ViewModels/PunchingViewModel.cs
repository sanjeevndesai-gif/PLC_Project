using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Models;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;

public enum MachineMode { Manual, Auto, Homing }

public partial class PunchingViewModel : ObservableObject
{
    private readonly IControllerService _controllerService;
    private readonly IDataStoreService _dataStoreService;

    [ObservableProperty] private bool _isConnected;

    // Axis positions
    [ObservableProperty] private double _posX;
    [ObservableProperty] private double _posY;

    // Target positions
    [ObservableProperty] private double _targetX;
    [ObservableProperty] private double _targetY;

    [ObservableProperty] private MachineMode _currentMode = MachineMode.Manual;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isEmergencyStop;

    [ObservableProperty] private string _programName = string.Empty;
    [ObservableProperty] private int _totalStrokes;
    [ObservableProperty] private int _completedStrokes;

    [ObservableProperty]
    private ObservableCollection<PunchProgram> _programs = new();

    [ObservableProperty]
    private PunchProgram? _selectedProgram;

    [ObservableProperty] private string _statusMessage = "Ready";

    [ObservableProperty] private int _selectedToolId = 1;

    private System.Timers.Timer? _positionUpdateTimer;

    public PunchingViewModel(IControllerService controllerService, IDataStoreService dataStoreService)
    {
        _controllerService = controllerService;
        _dataStoreService = dataStoreService;
        _controllerService.ConnectionStateChanged += OnConnectionStateChanged;
        IsConnected = controllerService.IsConnected;
        LoadSamplePrograms();
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        IsConnected = state == ConnectionState.Connected;
        if (IsConnected)
            StartPositionPolling();
        else
            StopPositionPolling();
    }

    private void LoadSamplePrograms()
    {
        var storedPrograms = _dataStoreService.LoadPunchPrograms();
        if (storedPrograms.Count > 0)
        {
            Programs = new ObservableCollection<PunchProgram>(storedPrograms);
            return;
        }

        Programs = new ObservableCollection<PunchProgram>
        {
            new() { ProgramId = 1, ProgramName = "PROG_001", Description = "Flange Pattern A", CreatedBy = "Admin",
                Steps = Enumerable.Range(1, 8).Select(i => new PunchStep { StepNumber = i, X = i * 50.0, Y = 100.0, ToolId = 1 }).ToList() },
            new() { ProgramId = 2, ProgramName = "PROG_002", Description = "Bracket Pattern B", CreatedBy = "Admin",
                Steps = Enumerable.Range(1, 12).Select(i => new PunchStep { StepNumber = i, X = i * 40.0, Y = 200.0, ToolId = 2 }).ToList() },
            new() { ProgramId = 3, ProgramName = "PROG_003", Description = "Panel Pattern C",  CreatedBy = "Operator",
                Steps = Enumerable.Range(1, 16).Select(i => new PunchStep { StepNumber = i, X = (i % 4) * 60.0, Y = (i / 4) * 60.0, ToolId = 1 }).ToList() },
        };

        _dataStoreService.SavePunchPrograms(Programs.ToList());
    }

    private void StartPositionPolling()
    {
        _positionUpdateTimer = new System.Timers.Timer(500);
        _positionUpdateTimer.Elapsed += async (_, _) => await UpdatePositions();
        _positionUpdateTimer.Start();
    }

    private void StopPositionPolling()
    {
        _positionUpdateTimer?.Stop();
        _positionUpdateTimer?.Dispose();
        _positionUpdateTimer = null;
    }

    private async Task UpdatePositions()
    {
        if (!IsConnected) return;
        PosX = await _controllerService.ReadRegisterAsync(100);
        PosY = await _controllerService.ReadRegisterAsync(101);
    }

    [RelayCommand]
    private void SetModeManual() { CurrentMode = MachineMode.Manual; StatusMessage = "Mode: Manual"; }

    [RelayCommand]
    private void SetModeAuto() { CurrentMode = MachineMode.Auto; StatusMessage = "Mode: Auto"; }

    [RelayCommand]
    private void HomeAxes()
    {
        CurrentMode = MachineMode.Homing;
        StatusMessage = "Homing all axes...";
        PosX = 0; PosY = 0;
        StatusMessage = "Homing complete.";
        CurrentMode = MachineMode.Manual;
    }

    [RelayCommand]
    private void StartProgram()
    {
        if (SelectedProgram is null) { StatusMessage = "No program selected."; return; }
        IsRunning = true;
        TotalStrokes = SelectedProgram.TotalStrokes;
        CompletedStrokes = 0;
        ProgramName = SelectedProgram.ProgramName;
        StatusMessage = $"Running {ProgramName}...";
    }

    [RelayCommand]
    private void StopProgram()
    {
        IsRunning = false;
        StatusMessage = "Program stopped.";
    }

    [RelayCommand]
    private void EmergencyStop()
    {
        IsRunning = false;
        IsEmergencyStop = true;
        StatusMessage = "⚠ EMERGENCY STOP ACTIVATED";
    }

    [RelayCommand]
    private void ResetEStop()
    {
        IsEmergencyStop = false;
        StatusMessage = "E-Stop reset. Ready.";
    }

    [RelayCommand]
    private void MoveToTarget()
    {
        if (!IsConnected) { StatusMessage = "Not connected."; return; }
        PosX = TargetX; PosY = TargetY;
        StatusMessage = $"Moved to X:{TargetX:F2}  Y:{TargetY:F2}";
    }

    [RelayCommand]
    private void SinglePunch()
    {
        if (!IsConnected) { StatusMessage = "Not connected."; return; }
        StatusMessage = $"Single punch at X:{PosX:F2}  Y:{PosY:F2} with Tool {SelectedToolId}";
        CompletedStrokes++;
    }
}

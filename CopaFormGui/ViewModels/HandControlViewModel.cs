using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;

public partial class HandControlViewModel : ObservableObject
{
    private readonly IControllerService _controllerService;

    [ObservableProperty] private bool _isConnected;

    // Current axis positions
    [ObservableProperty] private double _posX;
    [ObservableProperty] private double _posY;

    // Selected jog step size (mm)
    [ObservableProperty] private double _jogStep = 1.0;

    // Feed rate override 0–100 %
    [ObservableProperty] private int _feedOverride = 100;

    [ObservableProperty] private bool _isEmergencyStop;
    [ObservableProperty] private string _statusMessage = "Ready – select a step size and use jog buttons";

    // Available step sizes for the radio buttons
    public double[] StepSizes { get; } = { 0.01, 0.1, 1.0, 10.0, 100.0 };

    public HandControlViewModel(IControllerService controllerService)
    {
        _controllerService = controllerService;
        _controllerService.ConnectionStateChanged += OnConnectionStateChanged;
        IsConnected = controllerService.IsConnected;
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        IsConnected = state == ConnectionState.Connected;
    }

    // ── Jog X ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private void JogXPlus()
    {
        PosX += JogStep;
        StatusMessage = $"Jog X+ {JogStep:F3} mm  → X = {PosX:F3}";
    }

    [RelayCommand]
    private void JogXMinus()
    {
        PosX -= JogStep;
        StatusMessage = $"Jog X- {JogStep:F3} mm  → X = {PosX:F3}";
    }

    // ── Jog Y ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private void JogYPlus()
    {
        PosY += JogStep;
        StatusMessage = $"Jog Y+ {JogStep:F3} mm  → Y = {PosY:F3}";
    }

    [RelayCommand]
    private void JogYMinus()
    {
        PosY -= JogStep;
        StatusMessage = $"Jog Y- {JogStep:F3} mm  → Y = {PosY:F3}";
    }

    // ── Axis commands ────────────────────────────────────────────────────────

    [RelayCommand]
    private void HomeAll()
    {
        PosX = 0; PosY = 0;
        StatusMessage = "All axes homed to zero.";
    }

    [RelayCommand]
    private void HomeX() { PosX = 0; StatusMessage = "X axis homed."; }

    [RelayCommand]
    private void HomeY() { PosY = 0; StatusMessage = "Y axis homed."; }

    [RelayCommand]
    private void ZeroAll()
    {
        PosX = 0; PosY = 0;
        StatusMessage = "Work-coordinate origin set at current position.";
    }

    [RelayCommand]
    private void EmergencyStop()
    {
        IsEmergencyStop = true;
        StatusMessage = "⚠ EMERGENCY STOP ACTIVATED";
    }

    [RelayCommand]
    private void ResetEStop()
    {
        IsEmergencyStop = false;
        StatusMessage = "E-Stop reset. Ready.";
    }
}

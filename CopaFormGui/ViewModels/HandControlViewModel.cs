using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;
public partial class HandControlViewModel : ObservableObject
{
    private readonly DispatcherTimer _axisPollTimer;
    // PMAC Jog/Home global variable mapping
    public async Task SetJogVariableAsync(string variable, int value)
    {
        if (_controllerService.IsConnected)
            await _controllerService.WriteVariableAsync(variable, value);
    }

    public async Task JogXPlusDown() => await SetJogVariableAsync("X_JOG_PLUS", 1);
    public async Task JogXPlusUp()   => await SetJogVariableAsync("X_JOG_PLUS", 0);
    public async Task JogXMinusDown() => await SetJogVariableAsync("X_JOG_MINUS", 1);
    public async Task JogXMinusUp()   => await SetJogVariableAsync("X_JOG_MINUS", 0);
    public async Task JogYPlusDown() => await SetJogVariableAsync("Y_JOG_PLUS", 1);
    public async Task JogYPlusUp()   => await SetJogVariableAsync("Y_JOG_PLUS", 0);
    public async Task JogYMinusDown() => await SetJogVariableAsync("Y_JOG_MINUS", 1);
    public async Task JogYMinusUp()   => await SetJogVariableAsync("Y_JOG_MINUS", 0);

    public async Task HomeXAsync() => await SetJogVariableAsync("X_HOME", 1);
    public async Task HomeYAsync() => await SetJogVariableAsync("Y_HOME", 1);
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

    [ObservableProperty]
    private string _homeFeedrate;

    partial void OnHomeFeedrateChanged(string value)
    {
        // Try parse and send to PMAC
        if (double.TryParse(value, out var v))
        {
            _ = _controllerService.WriteVariableAsync("HOME_FEEDRATE", v);
        }
    }

    public HandControlViewModel(IControllerService controllerService)
    {
        _controllerService = controllerService;
        _controllerService.ConnectionStateChanged += OnConnectionStateChanged;
        IsConnected = controllerService.IsConnected;

        _axisPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _axisPollTimer.Tick += async (s, e) => await PollAxisPositionsAsync();
        _axisPollTimer.Start();
    }

    private async Task PollAxisPositionsAsync()
    {
        if (_controllerService.IsConnected)
        {
            var xRaw = await _controllerService.ReadResponseAsync("X_ABS_POS");
            var yRaw = await _controllerService.ReadResponseAsync("Y_ABS_POS");
            double xVal, yVal;
            bool xParsed = double.TryParse(xRaw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out xVal);
            bool yParsed = double.TryParse(yRaw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out yVal);
            string debugMsg = $"PMAC X_ABS_POS: {(xParsed ? xVal.ToString("F3") : "null")}, Y_ABS_POS: {(yParsed ? yVal.ToString("F3") : "null")}";
            if (xParsed)
                PosX = xVal;
            if (yParsed)
                PosY = yVal;
            StatusMessage = $"Connected | X: {PosX:F3} | Y: {PosY:F3} | {debugMsg}";
        }
        else
        {
            StatusMessage = "Not connected to PMAC";
        }
    // removed extra closing brace
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

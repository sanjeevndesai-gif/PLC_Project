using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Models;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;

public partial class AlarmViewModel : ObservableObject
{
    private static readonly string DataFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CopaFormGui");

    private static readonly string HistoryFilePath =
        Path.Combine(DataFolder, "alarm_history.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IControllerService _controllerService;
    private readonly List<AlarmSignal> _alarmSignals;
    private readonly Dictionary<string, bool> _lastSignalStates = new(StringComparer.OrdinalIgnoreCase);
    private System.Timers.Timer? _monitorTimer;

    [ObservableProperty] private bool _isConnected;

    [ObservableProperty]
    private ObservableCollection<AlarmRecord> _alarms = new();

    [ObservableProperty]
    private ObservableCollection<AlarmRecord> _activeAlarms = new();

    [ObservableProperty]
    private AlarmRecord? _selectedAlarm;

    [ObservableProperty] private string _statusMessage = "System Normal";

    [ObservableProperty] private bool _hasActiveAlarms;

    public AlarmViewModel(IControllerService controllerService)
    {
        _controllerService = controllerService;
        _alarmSignals = BuildAlarmSignalMap();
        _controllerService.ConnectionStateChanged += OnConnectionStateChanged;
        IsConnected = controllerService.IsConnected;
        LoadHistory();

        if (IsConnected)
            StartMonitoring();
    }

    private static List<AlarmSignal> BuildAlarmSignalMap()
    {
        return new List<AlarmSignal>
        {
            new("X_NLIMIT_UI", "X_NLIMIT_UI", "X AXIS NEGATIVE LIMIT REACHED", AlarmSeverity.Warning),
            new("X_PLIMIT_UI", "X_PLIMIT_UI", "X AXIS POSITIVE LIMIT REACHED", AlarmSeverity.Warning),
            new("Y_NLIMIT_UI", "Y_NLIMIT_UI", "Y AXIS NEGATIVE LIMIT REACHED", AlarmSeverity.Warning),
            new("Y_PLIMIT_UI", "Y_PLIMIT_UI", "Y AXIS POSITIVE LIMIT REACHED", AlarmSeverity.Warning),
            new("HYD_UP_SENSE_ERR", "HYD_UP_SENSE_ERR", "HYD UP SENSE ERROR", AlarmSeverity.Error, "HYD_UP_SENSE_ERR", "HYDRAULIC_UP_SENS_UI"),
            new("HYD_DOWN_SENSE_ERR", "HYD_DOWN_SENSE_ERR", "HYD DOWN SENSE ERROR", AlarmSeverity.Error, "HYD_DOWN_SENSE_ERR", "HYD_DOWN_SENSE", "HYDRAULIC_DOWN_SENS_UI"),
            new("X_AXIS_ERROR", "X_AXIS_ERROR", "X AXIS SERVO ERROR", AlarmSeverity.Error, "X_AXIS_ERROR_UI"),
            new("Y_AXIS_ERROR", "Y_AXIS_ERROR", "Y AXIS SERVO ERROR", AlarmSeverity.Error, "Y_AXIS_ERROR_UI"),
            new("JOGSPEED_LT_1", "JOGSPEED<1", "JOG SPEED IS ZERO", AlarmSeverity.Warning, "JOGSPEED_LT_1", "JOGSPEED_ZERO", "JOGSPEED<1"),
            new("EMERGENCY_PB=0", "EMERGENCY_PB=0", "EMERGENCY PB PRESSED", AlarmSeverity.Critical, "EMERGENCY_PB=0", "Emergency_PB=0", "EMERGENCY_PB_UI=0", "Emergency_PB_UI=0", "EMERGENCY_PB_UI", "Emergency_PB_UI"),
            new("BUSBAR_PRESENT_S_INS", "BUSBAR_PRESENT_S_INS", "INSERT BUSBAR TO START PUNCH", AlarmSeverity.Warning, "BUSBAR_PRESENT_S_INS", "BUSBAR_PRESENT_SENS_UI", "BUSBAR_PRESENT_SENSOR")
        };
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        _ = sender;
        IsConnected = state == ConnectionState.Connected;
        if (IsConnected)
        {
            StartMonitoring();
            _ = PollAlarmSignalsAsync();
        }
        else
        {
            StopMonitoring();
        }
    }

    private void StartMonitoring()
    {
        if (_monitorTimer is not null)
            return;

        _monitorTimer = new System.Timers.Timer(300);
        _monitorTimer.AutoReset = true;
        _monitorTimer.Elapsed += OnMonitorTimerElapsed;
        _monitorTimer.Start();
    }

    private void StopMonitoring()
    {
        if (_monitorTimer is null)
            return;

        _monitorTimer.Stop();
        _monitorTimer.Elapsed -= OnMonitorTimerElapsed;
        _monitorTimer.Dispose();
        _monitorTimer = null;
    }

    private async void OnMonitorTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        _ = sender;
        _ = e;

        try
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await PollAlarmSignalsAsync();
            });
        }
        catch
        {
            // Ignore polling exceptions to keep monitor alive.
        }
    }

    private async Task PollAlarmSignalsAsync()
    {
        if (!IsConnected)
            return;

        foreach (var signal in _alarmSignals)
        {
            var isActive = await TryReadAlarmSignalAsync(signal);
            var lastState = _lastSignalStates.TryGetValue(signal.SignalKey, out var previous) && previous;

            if (isActive && !lastState)
            {
                AddAlarm(new AlarmRecord
                {
                    Timestamp = DateTime.Now,
                    Code = signal.Code,
                    Description = signal.Description,
                    Severity = signal.Severity,
                    IsAcknowledged = false
                });
            }

            if (!isActive && lastState)
            {
                var lastOpen = Alarms
                    .Where(a => string.Equals(a.Code, signal.Code, StringComparison.OrdinalIgnoreCase) && !a.IsAcknowledged)
                    .OrderByDescending(a => a.Timestamp)
                    .FirstOrDefault();

                if (lastOpen is not null)
                {
                    lastOpen.IsAcknowledged = true;
                    lastOpen.AcknowledgedBy = "System";
                    RefreshActiveAlarms();
                    SaveHistory();
                }
            }

            _lastSignalStates[signal.SignalKey] = isActive;
        }
    }

    private async Task<bool> TryReadAlarmSignalAsync(AlarmSignal signal)
    {
        var variableCandidates = signal.VariableCandidates;
        for (var i = 0; i < variableCandidates.Length; i++)
        {
            var candidate = variableCandidates[i];
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            if (IsExpressionCandidate(candidate))
            {
                var response = await _controllerService.ReadResponseAsync($"echo 7 {candidate}");
                if (TryParseNumericFromResponse(response, out var expressionValue))
                    return Math.Abs(expressionValue) > 0.00001d;

                continue;
            }

            var value = await _controllerService.ReadVariableAsync(candidate);
            if (value.HasValue)
                return Math.Abs(value.Value) > 0.00001d;
        }

        return false;
    }

    private static bool IsExpressionCandidate(string candidate)
    {
        return candidate.Contains('<')
            || candidate.Contains('>')
            || candidate.Contains("==", StringComparison.Ordinal)
            || candidate.Contains("!=", StringComparison.Ordinal)
            || candidate.Contains('=');
    }

    private static bool TryParseNumericFromResponse(string? response, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(response))
            return false;

        var match = Regex.Match(response, @"[-+]?\d*\.?\d+");
        if (!match.Success)
            return false;

        return double.TryParse(match.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private void LoadHistory()
    {
        try
        {
            if (File.Exists(HistoryFilePath))
            {
                var json = File.ReadAllText(HistoryFilePath);
                var records = JsonSerializer.Deserialize<List<AlarmRecord>>(json);
                if (records is { Count: > 0 })
                {
                    Alarms = new ObservableCollection<AlarmRecord>(records);
                    RefreshActiveAlarms();
                    return;
                }
            }
        }
        catch { /* Fall through to seed data on error */ }

        SeedDefaultAlarms();
    }

    private void SaveHistory()
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            var json = JsonSerializer.Serialize(Alarms.ToList(), JsonOptions);
            File.WriteAllText(HistoryFilePath, json);
        }
        catch { /* Ignore save errors */ }
    }

    private void SeedDefaultAlarms()
    {
        Alarms = new ObservableCollection<AlarmRecord>();
        RefreshActiveAlarms();
        SaveHistory();
    }

    private void RefreshActiveAlarms()
    {
        ActiveAlarms = new ObservableCollection<AlarmRecord>(Alarms.Where(a => !a.IsAcknowledged));
        HasActiveAlarms = ActiveAlarms.Any();
        StatusMessage = HasActiveAlarms ? $"{ActiveAlarms.Count} active alarm(s)" : "System Normal";
    }

    [RelayCommand]
    private void AcknowledgeSelected()
    {
        if (SelectedAlarm is null) return;
        SelectedAlarm.IsAcknowledged = true;
        SelectedAlarm.AcknowledgedBy = "Operator";
        RefreshActiveAlarms();
        SaveHistory();
        StatusMessage = $"Alarm {SelectedAlarm.Code} acknowledged.";
    }

    [RelayCommand]
    private void AcknowledgeAll()
    {
        foreach (var alarm in Alarms)
        {
            alarm.IsAcknowledged = true;
            alarm.AcknowledgedBy = "Operator";
        }
        RefreshActiveAlarms();
        SaveHistory();
        StatusMessage = "All alarms acknowledged.";
    }

    [RelayCommand]
    private void ClearHistory()
    {
        Alarms.Clear();
        RefreshActiveAlarms();
        SaveHistory();
        StatusMessage = "Alarm history cleared.";
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadHistory();
        StatusMessage = "Alarm list refreshed.";
    }

    public void AddAlarm(AlarmRecord alarm)
    {
        alarm.AlarmId = Alarms.Count > 0 ? Alarms.Max(a => a.AlarmId) + 1 : 1;
        Alarms.Add(alarm);
        RefreshActiveAlarms();
        SaveHistory();
    }

    private sealed class AlarmSignal
    {
        public AlarmSignal(string signalKey, string code, string description, AlarmSeverity severity, params string[] variableCandidates)
        {
            SignalKey = signalKey;
            Code = code;
            Description = description;
            Severity = severity;
            VariableCandidates = variableCandidates.Length > 0 ? variableCandidates : new[] { signalKey };
        }

        public string SignalKey { get; }
        public string Code { get; }
        public string Description { get; }
        public AlarmSeverity Severity { get; }
        public string[] VariableCandidates { get; }
    }
}

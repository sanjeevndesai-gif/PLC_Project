using System.Collections.ObjectModel;
using System.IO;
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
    private readonly Dictionary<string, AlarmRecord> _liveActiveAlarms = new(StringComparer.OrdinalIgnoreCase);
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
        {
            StartMonitoring();
            _ = PollAlarmSignalsAsync(captureBaselineOnly: true);
        }
    }

    private static List<AlarmSignal> BuildAlarmSignalMap()
    {
        return new List<AlarmSignal>
        {
            new(1, "X_NLIMIT_UI", "X_NLIMIT_UI", "X AXIS NEGATIVE LIMIT REACHED", AlarmSeverity.Warning),
            new(2, "X_PLIMIT_UI", "X_PLIMIT_UI", "X AXIS POSITIVE LIMIT REACHED", AlarmSeverity.Warning),
            new(3, "Y_NLIMIT_UI", "Y_NLIMIT_UI", "Y AXIS NEGATIVE LIMIT REACHED", AlarmSeverity.Warning),
            new(4, "Y_PLIMIT_UI", "Y_PLIMIT_UI", "Y AXIS POSITIVE LIMIT REACHED", AlarmSeverity.Warning),
            new(5, "HYD_UP_SENSE_ERR", "HYD_UP_SENSE_ERR", "HYD UP SENSE ERROR", AlarmSeverity.Error, "HYD_UP_SENSE_ERR", "HYDRAULIC_UP_SENS_UI"),
            new(6, "HYD_DOWN_SENSE_ERR", "HYD_DOWN_SENSE_ERR", "HYD DOWN SENSE ERROR", AlarmSeverity.Error, "HYD_DOWN_SENSE_ERR", "HYD_DOWN_SENSE", "HYDRAULIC_DOWN_SENS_UI"),
            new(7, "X_AXIS_ERROR", "X_AXIS_ERROR", "X AXIS SERVO ERROR", AlarmSeverity.Error, "X_AXIS_ERROR_UI"),
            new(8, "Y_AXIS_ERROR", "Y_AXIS_ERROR", "Y AXIS SERVO ERROR", AlarmSeverity.Error, "Y_AXIS_ERROR_UI"),
            new(9, "JOGSPEED_LT_1", "JOGSPEED<1", "JOG SPEED IS ZERO", AlarmSeverity.Warning, "JOGSPEED_LT_1", "JOGSPEED_ZERO", "JOGSPEED<1"),
            new(10, "EMERGENCY_PB=0", "EMERGENCY_PB=0", "EMERGENCY PB PRESSED", AlarmSeverity.Critical, "EMERGENCY_PB=0", "Emergency_PB=0", "EMERGENCY_PB_UI=0", "Emergency_PB_UI=0", "EMERGENCY_PB_UI", "Emergency_PB_UI"),
            new(11, "BUSBAR_PRESENT_S_INS", "BUSBAR_PRESENT_S_INS", "INSERT BUSBAR TO START PUNCH", AlarmSeverity.Warning, "BUSBAR_PRESENT_S_INS", "BUSBAR_PRESENT_SENS_UI", "BUSBAR_PRESENT_SENSOR")
        };
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        _ = sender;
        IsConnected = state == ConnectionState.Connected;
        if (IsConnected)
        {
            StartMonitoring();
            _ = PollAlarmSignalsAsync(captureBaselineOnly: true);
        }
        else
        {
            StopMonitoring();
            _liveActiveAlarms.Clear();
            RefreshActiveAlarms();
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
                await PollAlarmSignalsAsync(captureBaselineOnly: false);
            });
        }
        catch
        {
            // Ignore polling exceptions to keep monitor alive.
        }
    }

    private async Task PollAlarmSignalsAsync(bool captureBaselineOnly)
    {
        if (!IsConnected)
            return;

        foreach (var signal in _alarmSignals)
        {
            var isActive = await TryReadAlarmSignalAsync(signal);
            var lastState = _lastSignalStates.TryGetValue(signal.SignalKey, out var previous) && previous;

            if (isActive)
            {
                if (!_liveActiveAlarms.ContainsKey(signal.SignalKey))
                {
                    var record = new AlarmRecord
                    {
                        AlarmId = signal.DisplayId,
                        Timestamp = DateTime.Now,
                        Code = signal.Code,
                        Description = signal.Description,
                        Severity = signal.Severity,
                        IsAcknowledged = false
                    };
                    _liveActiveAlarms[signal.SignalKey] = record;
                    AddAlarm(record);
                }
                else if (!captureBaselineOnly && !lastState)
                {
                    var record = new AlarmRecord
                    {
                        AlarmId = signal.DisplayId,
                        Timestamp = DateTime.Now,
                        Code = signal.Code,
                        Description = signal.Description,
                        Severity = signal.Severity,
                        IsAcknowledged = false
                    };
                    AddAlarm(record);
                    _liveActiveAlarms[signal.SignalKey] = record;
                }
            }

            if (!isActive && lastState)
            {
                _liveActiveAlarms.Remove(signal.SignalKey);

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

        RefreshActiveAlarms();
    }

    private async Task<bool> TryReadAlarmSignalAsync(AlarmSignal signal)
    {
        var variableCandidates = signal.VariableCandidates;
        for (var i = 0; i < variableCandidates.Length; i++)
        {
            var candidate = variableCandidates[i];
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            var value = await _controllerService.ReadVariableAsync(candidate);
            if (value.HasValue)
                return Math.Abs(value.Value) > 0.00001d;
        }

        return false;
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
        ActiveAlarms = new ObservableCollection<AlarmRecord>(
            _liveActiveAlarms.Values.OrderByDescending(a => a.Timestamp));
        HasActiveAlarms = ActiveAlarms.Any();
        StatusMessage = HasActiveAlarms ? $"{ActiveAlarms.Count} active alarm(s)" : "System Normal";
    }

    [RelayCommand]
    private void AcknowledgeSelected()
    {
        if (SelectedAlarm is null) return;

        var matchingSignals = _alarmSignals
            .Where(s => string.Equals(s.Code, SelectedAlarm.Code, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.SignalKey)
            .ToList();

        for (var i = 0; i < matchingSignals.Count; i++)
            _liveActiveAlarms.Remove(matchingSignals[i]);

        var lastOpen = Alarms
            .Where(a => string.Equals(a.Code, SelectedAlarm.Code, StringComparison.OrdinalIgnoreCase) && !a.IsAcknowledged)
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefault();

        if (lastOpen is not null)
        {
            lastOpen.IsAcknowledged = true;
            lastOpen.AcknowledgedBy = "Operator";
        }

        RefreshActiveAlarms();
        SaveHistory();
        StatusMessage = $"Alarm {SelectedAlarm.Code} acknowledged.";
    }

    [RelayCommand]
    private void AcknowledgeAll()
    {
        _liveActiveAlarms.Clear();

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
        SaveHistory();
        _liveActiveAlarms.Clear();
        RefreshActiveAlarms();
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
        public AlarmSignal(int displayId, string signalKey, string code, string description, AlarmSeverity severity, params string[] variableCandidates)
        {
            DisplayId = displayId;
            SignalKey = signalKey;
            Code = code;
            Description = description;
            Severity = severity;
            VariableCandidates = variableCandidates.Length > 0 ? variableCandidates : new[] { signalKey };
        }

        public int DisplayId { get; }
        public string SignalKey { get; }
        public string Code { get; }
        public string Description { get; }
        public AlarmSeverity Severity { get; }
        public string[] VariableCandidates { get; }
    }
}

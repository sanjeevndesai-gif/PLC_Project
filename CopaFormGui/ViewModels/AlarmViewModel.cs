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
        _controllerService.ConnectionStateChanged += (_, s) => IsConnected = s == ConnectionState.Connected;
        IsConnected = controllerService.IsConnected;
        LoadHistory();
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
        Alarms = new ObservableCollection<AlarmRecord>
        {
            new() { AlarmId = 1, Code = "AL001", Description = "X-Axis Limit Switch Triggered",    Severity = AlarmSeverity.Warning,  Timestamp = DateTime.Now.AddHours(-2),    IsAcknowledged = true,  AcknowledgedBy = "Operator" },
            new() { AlarmId = 2, Code = "AL002", Description = "Z-Axis Encoder Error",             Severity = AlarmSeverity.Error,    Timestamp = DateTime.Now.AddHours(-1),    IsAcknowledged = false },
            new() { AlarmId = 3, Code = "AL003", Description = "Tool 5 Stroke Count Exceeded",     Severity = AlarmSeverity.Warning,  Timestamp = DateTime.Now.AddMinutes(-30), IsAcknowledged = false },
            new() { AlarmId = 4, Code = "AL004", Description = "Emergency Stop Activated",          Severity = AlarmSeverity.Critical, Timestamp = DateTime.Now.AddMinutes(-15), IsAcknowledged = true,  AcknowledgedBy = "Admin" },
            new() { AlarmId = 5, Code = "AL005", Description = "Controller Communication Timeout", Severity = AlarmSeverity.Error,    Timestamp = DateTime.Now.AddMinutes(-5),  IsAcknowledged = false },
            new() { AlarmId = 6, Code = "AL006", Description = "Clamp Pressure Low",               Severity = AlarmSeverity.Warning,  Timestamp = DateTime.Now.AddMinutes(-2),  IsAcknowledged = false },
            new() { AlarmId = 7, Code = "AL007", Description = "Program Cycle Complete",           Severity = AlarmSeverity.Info,     Timestamp = DateTime.Now.AddMinutes(-1),  IsAcknowledged = true,  AcknowledgedBy = "Operator" },
        };
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
}

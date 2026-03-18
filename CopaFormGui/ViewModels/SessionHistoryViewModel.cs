using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Models;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;

public partial class SessionHistoryViewModel : ObservableObject
{
    private readonly ISessionService _sessionService;
    private readonly IDataStoreService _dataStoreService;

    [ObservableProperty]
    private ObservableCollection<SessionRecord> _sessions = new();

    [ObservableProperty]
    private SessionRecord? _selectedSession;

    [ObservableProperty]
    private string _statusMessage = "Session History";

    [ObservableProperty]
    private string _activeSessionInfo = "No active session";

    [ObservableProperty]
    private bool _hasActiveSession;

    public SessionHistoryViewModel(ISessionService sessionService, IDataStoreService dataStoreService)
    {
        _sessionService = sessionService;
        _dataStoreService = dataStoreService;
        _sessionService.ActiveSessionChanged += OnActiveSessionChanged;
        RefreshSessions();
        UpdateActiveSessionInfo();
    }

    private void OnActiveSessionChanged(object? sender, SessionRecord? session)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            UpdateActiveSessionInfo();
            RefreshSessions();
        });
    }

    private void UpdateActiveSessionInfo()
    {
        var session = _sessionService.ActiveSession;
        HasActiveSession = session is not null;
        ActiveSessionInfo = session is not null
            ? $"Active: {session.OperatorName}  |  Started: {session.StartTime:HH:mm:ss}  |  Punches: {session.TotalPunches}"
            : "No active session";
    }

    [RelayCommand]
    private void Refresh()
    {
        RefreshSessions();
        UpdateActiveSessionInfo();
        StatusMessage = "Refreshed.";
    }

    [RelayCommand]
    private void ClearHistory()
    {
        // Only clears completed (persisted) sessions. The active in-memory session is unaffected
        // and will be saved normally when EndSession() is called.
        _dataStoreService.SaveSessions(new List<SessionRecord>());
        RefreshSessions();
        StatusMessage = "Session history cleared.";
    }

    private void RefreshSessions()
    {
        var history = _sessionService.GetSessionHistory();
        Sessions = new ObservableCollection<SessionRecord>(history.OrderByDescending(s => s.StartTime));
        StatusMessage = $"{Sessions.Count} session(s) in history";
    }
}

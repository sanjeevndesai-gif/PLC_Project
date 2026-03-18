using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IControllerService _controllerService;
    private readonly ISessionService _sessionService;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatusText = "Disconnected";

    [ObservableProperty]
    private ObservableObject? _currentView;

    [ObservableProperty]
    private string _currentViewName = "Home";

    [ObservableProperty]
    private string _statusBarMessage = "Ready";

    private readonly OverviewViewModel _overviewViewModel;
    private readonly DatabaseViewModel _databaseViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly PunchingViewModel _punchingViewModel;
    private readonly HandControlViewModel _handControlViewModel;
    private readonly AlarmViewModel _alarmViewModel;
    private readonly ToolManagementViewModel _toolManagementViewModel;
    private readonly IOMonitorViewModel _ioMonitorViewModel;
    private readonly ProgramEditorViewModel _programEditorViewModel;
    private readonly SessionHistoryViewModel _sessionHistoryViewModel;

    public MainViewModel(
        IControllerService controllerService,
        ISessionService sessionService,
        OverviewViewModel overviewViewModel,
        DatabaseViewModel databaseViewModel,
        SettingsViewModel settingsViewModel,
        PunchingViewModel punchingViewModel,
        HandControlViewModel handControlViewModel,
        AlarmViewModel alarmViewModel,
        ToolManagementViewModel toolManagementViewModel,
        IOMonitorViewModel ioMonitorViewModel,
        ProgramEditorViewModel programEditorViewModel,
        SessionHistoryViewModel sessionHistoryViewModel)
    {
        _controllerService = controllerService;
        _sessionService = sessionService;
        _overviewViewModel = overviewViewModel;
        _databaseViewModel = databaseViewModel;
        _settingsViewModel = settingsViewModel;
        _punchingViewModel = punchingViewModel;
        _handControlViewModel = handControlViewModel;
        _alarmViewModel = alarmViewModel;
        _toolManagementViewModel = toolManagementViewModel;
        _ioMonitorViewModel = ioMonitorViewModel;
        _programEditorViewModel = programEditorViewModel;
        _sessionHistoryViewModel = sessionHistoryViewModel;

        _controllerService.ConnectionStateChanged += OnConnectionStateChanged;
        IsConnected = _controllerService.IsConnected;
        UpdateConnectionStatus();

        CurrentView = _overviewViewModel;
        CurrentViewName = "Overview";
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        IsConnected = state == ConnectionState.Connected;
        UpdateConnectionStatus();
    }

    private void UpdateConnectionStatus()
    {
        ConnectionStatusText = IsConnected ? "Connected" : "Disconnected";
    }

    [RelayCommand]
    private void ShowOverview()
    {
        CurrentView = _overviewViewModel;
        CurrentViewName = "Overview";
        StatusBarMessage = "Machine Overview";
    }

    [RelayCommand]
    private void ShowPunching()
    {
        CurrentView = _punchingViewModel;
        CurrentViewName = "Punching";
        StatusBarMessage = "Punching Operations";
    }

    [RelayCommand]
    private void ShowHandControl()
    {
        CurrentView = _handControlViewModel;
        CurrentViewName = "Hand Control";
        StatusBarMessage = "Hand Control – Manual Jog";
    }

    [RelayCommand]
    private void ShowDatabase()
    {
        CurrentView = _databaseViewModel;
        CurrentViewName = "Database";
        StatusBarMessage = "Database – Operator will enter all the data";
    }

    [RelayCommand]
    private void ShowSettings()
    {
        CurrentView = _settingsViewModel;
        CurrentViewName = "Settings";
        StatusBarMessage = "Machine Settings";
    }

    [RelayCommand]
    private void ShowAlarms()
    {
        CurrentView = _alarmViewModel;
        CurrentViewName = "Alarms";
        StatusBarMessage = "Alarm History";
    }

    [RelayCommand]
    private void ShowToolManagement()
    {
        CurrentView = _toolManagementViewModel;
        CurrentViewName = "Tool Management";
        StatusBarMessage = "Tool Management";
    }

    [RelayCommand]
    private void ShowIOMonitor()
    {
        CurrentView = _ioMonitorViewModel;
        CurrentViewName = "I/O Monitor";
        StatusBarMessage = "PLC Digital I/O Monitor";
    }

    [RelayCommand]
    private void ShowProgramEditor()
    {
        try
        {
            App.LogInfo("Navigating to Program Editor");
            CurrentView = _programEditorViewModel;
            CurrentViewName = "Program Editor";
            StatusBarMessage = "CNC Punch Program Editor";
            App.LogInfo("Program Editor navigation completed");
        }
        catch (Exception ex)
        {
            App.LogException("ShowProgramEditor", ex);
            StatusBarMessage = "Program Editor open failed. Check app log.";
        }
    }

    [RelayCommand]
    private void ShowSessionHistory()
    {
        CurrentView = _sessionHistoryViewModel;
        CurrentViewName = "Session History";
        StatusBarMessage = "Operator Session History";
    }

    partial void OnCurrentViewChanged(ObservableObject? value)
    {
        App.LogInfo($"CurrentView changed: {value?.GetType().Name ?? "null"}");
    }

    [RelayCommand]
    private void Exit()
    {
        _sessionService.EndSession();
        _controllerService.Disconnect();
        System.Windows.Application.Current.Shutdown();
    }
}

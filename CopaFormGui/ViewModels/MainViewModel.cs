using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IControllerService _controllerService;

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

    private readonly DatabaseViewModel _databaseViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly PunchingViewModel _punchingViewModel;
    private readonly AlarmViewModel _alarmViewModel;
    private readonly ToolManagementViewModel _toolManagementViewModel;

    public MainViewModel(
        IControllerService controllerService,
        DatabaseViewModel databaseViewModel,
        SettingsViewModel settingsViewModel,
        PunchingViewModel punchingViewModel,
        AlarmViewModel alarmViewModel,
        ToolManagementViewModel toolManagementViewModel)
    {
        _controllerService = controllerService;
        _databaseViewModel = databaseViewModel;
        _settingsViewModel = settingsViewModel;
        _punchingViewModel = punchingViewModel;
        _alarmViewModel = alarmViewModel;
        _toolManagementViewModel = toolManagementViewModel;

        _controllerService.ConnectionStateChanged += OnConnectionStateChanged;
        IsConnected = _controllerService.IsConnected;
        UpdateConnectionStatus();

        CurrentView = _punchingViewModel;
        CurrentViewName = "Punching";
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
    private void ShowPunching()
    {
        CurrentView = _punchingViewModel;
        CurrentViewName = "Punching";
        StatusBarMessage = "Punching Operations";
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
    private void Exit()
    {
        _controllerService.Disconnect();
        System.Windows.Application.Current.Shutdown();
    }
}

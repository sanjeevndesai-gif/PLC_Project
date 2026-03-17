using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IControllerService _controllerService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string _ipAddress = "192.168.0.200";

    [ObservableProperty]
    private string _userName = "root";

    [ObservableProperty]
    private string _password = "deltatau";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _hasError;

    public event EventHandler<bool>? LoginCompleted; // true = connected, false = no device

    public LoginViewModel(IControllerService controllerService, ISettingsService settingsService)
    {
        _controllerService = controllerService;
        _settingsService = settingsService;

        var saved = _settingsService.LoadConnectionSettings();
        IpAddress = saved.IpAddress;
        UserName = saved.UserName;
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        IsConnecting = true;
        HasError = false;
        StatusMessage = "Connecting...";

        var connected = await _controllerService.ConnectAsync(IpAddress, UserName, Password);

        IsConnecting = false;

        if (connected)
        {
            _settingsService.SaveConnectionSettings(new Models.ConnectionSettings
            {
                IpAddress = IpAddress,
                UserName = UserName,
                Password = Password
            });
            StatusMessage = "Connected successfully.";
            LoginCompleted?.Invoke(this, true);
        }
        else
        {
            HasError = true;
            StatusMessage = "Unable to Connect – Check for Connection";
        }
    }

    private bool CanConnect() => !IsConnecting;

    [RelayCommand]
    private void NoDevice()
    {
        StatusMessage = "Opening without device...";
        LoginCompleted?.Invoke(this, false);
    }

    partial void OnIsConnectingChanged(bool value) => ConnectCommand.NotifyCanExecuteChanged();
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IControllerService _controllerService;
    private readonly ISettingsService _settingsService;
    private readonly ISessionService _sessionService;

    [ObservableProperty]
    private string _ipAddress = "172.20.0.200";

    [ObservableProperty]
    private string _userName = "root";

    [ObservableProperty]
    private string _password = "deltatau";

    [ObservableProperty]
    private string _operatorName = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private bool _hasError;

    public event EventHandler<bool>? LoginCompleted; // true = connected, false = no device

    public LoginViewModel(IControllerService controllerService, ISettingsService settingsService, ISessionService sessionService)
    {
        _controllerService = controllerService;
        _settingsService = settingsService;
        _sessionService = sessionService;

        var saved = _settingsService.LoadConnectionSettings();
        IpAddress = saved.IpAddress;
        UserName = saved.UserName;
        Password = saved.Password;
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        await ConnectFromUiAsync();
    }

    public async Task ConnectFromUiAsync()
    {
        if (!CanConnect()) return;

        IsConnecting = true;
        HasError = false;
        StatusMessage = "Connecting...";
        await Task.Yield(); // let WPF render the progress bar before blocking work starts

        try
        {
            var connected = await _controllerService.ConnectAsync(IpAddress, UserName, Password ?? string.Empty);

            if (connected)
            {
                _settingsService.SaveConnectionSettings(new Models.ConnectionSettings
                {
                    IpAddress = IpAddress,
                    UserName = UserName,
                    Password = Password ?? string.Empty
                });
                _sessionService.StartSession(OperatorName);
                StatusMessage = "Connected successfully.";
                LoginCompleted?.Invoke(this, true);
            }
            else
            {
                HasError = true;
                StatusMessage = string.IsNullOrWhiteSpace(_controllerService.LastConnectionError)
                    ? $"Cannot connect to PLC at {IpAddress}:22 — Check IP address, network cable, and that the PLC is powered on. Please try again."
                    : _controllerService.LastConnectionError + "\n\nPlease check the connection and try again.";
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = $"Connection error: {ex.Message}";
            App.LogException("LoginViewModel.ConnectFromUiAsync", ex);
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private bool CanConnect() => !IsConnecting;

    [RelayCommand]
    private void NoDevice()
    {
        StatusMessage = "Opening without device...";
        _sessionService.StartSession(OperatorName);
        LoginCompleted?.Invoke(this, false);
    }

    partial void OnIsConnectingChanged(bool value) => ConnectCommand.NotifyCanExecuteChanged();
}

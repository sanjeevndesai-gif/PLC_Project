using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IControllerService _controllerService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string _ipAddress = "172.20.0.200";

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
    private CancellationTokenSource? _offlineWatcherCts;

    public LoginViewModel(IControllerService controllerService, ISettingsService settingsService)
    {
        _controllerService = controllerService;
        _settingsService = settingsService;

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
                StatusMessage = "Connected successfully.";
                CancelOfflineWatcher();
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
        // Ensure controller treats this session as offline (do not enable TCP heartbeat checks)
        try { (_controllerService as ControllerService)?.SetUserInitiatedConnection(false); } catch { }
        LoginCompleted?.Invoke(this, false);
        // Start background watcher that will attempt to auto-connect
        StartOfflineAutoConnectWatcher();
    }

    private void StartOfflineAutoConnectWatcher()
    {
        // If there's already a watcher running, leave it.
        if (_offlineWatcherCts != null && !_offlineWatcherCts.IsCancellationRequested) return;

        _offlineWatcherCts = new CancellationTokenSource();
        var token = _offlineWatcherCts.Token;

        Task.Run(async () =>
        {
            try
            {
                // Check every 5 seconds until cancelled or connected
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (_controllerService.IsConnected)
                        {
                            // Already connected; raise event and stop watcher
                            LoginCompleted?.Invoke(this, true);
                            break;
                        }

                        var saved = _settingsService.LoadConnectionSettings();
                        if (string.IsNullOrWhiteSpace(saved.IpAddress) || string.IsNullOrWhiteSpace(saved.UserName))
                        {
                            await Task.Delay(5000, token);
                            continue;
                        }

                        // Attempt to connect using saved credentials (silent background attempt)
                        var ok = await _controllerService.ConnectSilentlyAsync(saved.IpAddress, saved.UserName, saved.Password ?? string.Empty);
                        if (ok)
                        {
                            CancelOfflineWatcher();
                            LoginCompleted?.Invoke(this, true);
                            break;
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch { /* ignore transient errors and retry */ }

                    await Task.Delay(5000, token);
                }
            }
            finally
            {
                // ensure CTS disposed
                _offlineWatcherCts?.Dispose();
                _offlineWatcherCts = null;
            }
        }, token);
    }

    private void CancelOfflineWatcher()
    {
        try
        {
            if (_offlineWatcherCts == null) return;
            _offlineWatcherCts.Cancel();
            _offlineWatcherCts.Dispose();
            _offlineWatcherCts = null;
        }
        catch { }
    }
    partial void OnIsConnectingChanged(bool value) => ConnectCommand.NotifyCanExecuteChanged();
}

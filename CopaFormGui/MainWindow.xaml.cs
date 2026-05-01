using System.Text;
using System.Windows;
using CopaFormGui.Services;
using CopaFormGui.ViewModels;

namespace CopaFormGui;

public partial class MainWindow : Window
{
    // Diagnostic popups disabled for end users
    private const bool ShowConnectionDiagnostics = false;

    private bool _reconnectAlertShown;
    private readonly IControllerService _controllerService;

    public MainWindow(MainViewModel viewModel, IControllerService controllerService)
    {
        InitializeComponent();
        DataContext = viewModel;

        _controllerService = controllerService;
        _controllerService.ConnectionStateChanged += OnConnectionStateChanged;
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        if (!ShowConnectionDiagnostics) return;

        string title, msg;
        MessageBoxImage icon;

        switch (state)
        {
            case ConnectionState.Connected:
                _reconnectAlertShown = false; // reset so next disconnect shows popup again
                title = "PMAC Connected";
                msg   = "Power PMAC is now CONNECTED.\n\nHeartbeat monitor is running.";
                icon  = MessageBoxImage.Information;
                break;
            case ConnectionState.Reconnecting:
                if (_reconnectAlertShown) return; // show only once per disconnect event
                _reconnectAlertShown = true;
                title = "PMAC Signal Lost";
                msg   = "Connection to Power PMAC was lost.\n\n" +
                        "Auto-reconnect is active — the app will retry every 5 seconds.\n" +
                        "Status bar will update to \"Connected\" once the link is restored.";
                icon  = MessageBoxImage.Warning;
                break;
            default:
                return;
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            MessageBox.Show(this, msg, title, MessageBoxButton.OK, icon);
        }));
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "DICS – CNC Punching Machine Controller Interface\nVersion 1.0\n\n© 2026 DICS",
            "About DICS",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
using System.Windows;
using System.Windows.Input;
using CopaFormGui.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CopaFormGui.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.LoginCompleted += OnLoginCompleted;

        // Populate PasswordBox from saved settings (Password property isn't bindable)
        Loaded += (_, _) => PasswordBox.Password = _viewModel.Password ?? string.Empty;
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.Password = PasswordBox.Password;
    }

    private void OnLoginCompleted(object? sender, bool isConnected)
    {
        Dispatcher.Invoke(() =>
        {
            var mainWindow = App.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
            Close();
        });
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ConnectButton.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;
            await _viewModel.ConnectFromUiAsync();

            if (_viewModel.HasError)
            {
                MessageBox.Show(
                    $"Controller not connected.\n\n{_viewModel.StatusMessage}\n\n" +
                    $"Please check:\n" +
                    $"  • IP Address: {_viewModel.IpAddress}\n" +
                    $"  • Port: 22 (SSH)\n" +
                    $"  • Network cable is connected\n" +
                    $"  • PLC is powered on",
                    "Connection Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            App.LogException("Login Connect button click", ex);
            MessageBox.Show(
                $"Connect failed unexpectedly.\n\n{ex.Message}",
                "Connect Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            ConnectButton.IsEnabled = true;
        }
    }
}

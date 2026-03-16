using System.Windows;
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

        // Wire up PasswordBox (WPF PasswordBox doesn't support binding natively)
        PasswordBox.Password = "deltatau";
        PasswordBox.PasswordChanged += (_, _) => _viewModel.Password = PasswordBox.Password;
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
}

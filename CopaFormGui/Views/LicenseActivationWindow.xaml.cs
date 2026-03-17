using System.Windows;
using CopaFormGui.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CopaFormGui.Views;

public partial class LicenseActivationWindow : Window
{
    private readonly LicenseActivationViewModel _viewModel;

    public LicenseActivationWindow(LicenseActivationViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.ActivationCompleted += OnActivationCompleted;
        Closed += (_, _) => _viewModel.ActivationCompleted -= OnActivationCompleted;
    }

    private void OnActivationCompleted(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var loginWindow = App.Services.GetRequiredService<LoginWindow>();
            loginWindow.Show();
            Close();
        });
    }
}

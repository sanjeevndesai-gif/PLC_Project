using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using CopaFormGui.Services;
using CopaFormGui.ViewModels;
using CopaFormGui.Views;

namespace CopaFormGui;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        var loginWindow = Services.GetRequiredService<LoginWindow>();
        loginWindow.Show();
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        // Services
        services.AddSingleton<IControllerService, ControllerService>();
        services.AddSingleton<ISettingsService, SettingsService>();

        // ViewModels
        services.AddSingleton<LoginViewModel>();
        services.AddSingleton<OverviewViewModel>();
        services.AddSingleton<DatabaseViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<PunchingViewModel>();
        services.AddSingleton<HandControlViewModel>();
        services.AddSingleton<AlarmViewModel>();
        services.AddSingleton<ToolManagementViewModel>();
        services.AddSingleton<IOMonitorViewModel>();
        services.AddSingleton<ProductionViewModel>();
        services.AddSingleton<ProgramEditorViewModel>();
        services.AddSingleton<MainViewModel>();

        // Views
        services.AddTransient<LoginWindow>();
        services.AddSingleton<MainWindow>();
    }
}


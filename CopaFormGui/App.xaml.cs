using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CopaFormGui.Services;
using CopaFormGui.ViewModels;
using CopaFormGui.Views;

namespace CopaFormGui;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    private static readonly object LogLock = new();
    private static readonly string LogDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CopaFormGui", "logs");
    private static readonly string LogFilePath = Path.Combine(LogDirectory, "app.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        Directory.CreateDirectory(LogDirectory);
        RegisterGlobalExceptionHandlers();
        LogInfo("Application startup");

        base.OnStartup(e);

        // Stabilize 3D preview rendering on systems with problematic GPU drivers.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        var licenseService = Services.GetRequiredService<ILicenseService>();
        var licenseResult = licenseService.ValidateCurrentMachineLicense();
        if (!licenseResult.IsValid)
        {
            MessageBox.Show(
                $"Application is not licensed for this machine.\n\n{licenseResult.ErrorMessage}",
                "License Required",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        var loginWindow = Services.GetRequiredService<LoginWindow>();
        loginWindow.Show();
    }

    public static void LogInfo(string message)
    {
        WriteLog("INFO", message);
    }

    public static void LogException(string source, Exception ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Source: {source}");
        sb.AppendLine($"Type: {ex.GetType().FullName}");
        sb.AppendLine($"Message: {ex.Message}");
        sb.AppendLine("StackTrace:");
        sb.AppendLine(ex.StackTrace ?? "<none>");
        if (ex.InnerException is not null)
        {
            sb.AppendLine("InnerException:");
            sb.AppendLine(ex.InnerException.ToString());
        }
        WriteLog("ERROR", sb.ToString());
    }

    private static void WriteLog(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
        lock (LogLock)
        {
            File.AppendAllText(LogFilePath, line);
        }
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException("DispatcherUnhandledException", e.Exception);
        MessageBox.Show(
            $"Unexpected error occurred.\n\n{e.Exception.Message}\n\nLog: {LogFilePath}",
            "Application Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            LogException("AppDomainUnhandledException", ex);
        else
            WriteLog("ERROR", $"AppDomainUnhandledException non-exception object: {e.ExceptionObject}");
    }

    private void OnTaskSchedulerUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogException("TaskSchedulerUnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        // Services
        services.AddSingleton<IControllerService, ControllerService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IDataStoreService, DataStoreService>();
        services.AddSingleton<ILicenseService, LicenseService>();
        services.AddSingleton<ISessionService, SessionService>();

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
        services.AddSingleton<ProgramEditorViewModel>();
        services.AddSingleton<SessionHistoryViewModel>();
        services.AddSingleton<MainViewModel>();

        // Views
        services.AddTransient<LoginWindow>();
        services.AddSingleton<MainWindow>();
    }
}


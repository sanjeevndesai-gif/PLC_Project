using System.Windows;
using System.Security.Cryptography;
using System.Text;

namespace CopaFormGui.Views
{
    public partial class PasswordPrompt : Window
    {
        private const string PlainPassword = "Dics@1996"; // used only to derive the stored hash at runtime

        public PasswordPrompt()
        {
            InitializeComponent();
            Closing += OnWindowClosing;
        }

        private static string ComputeSha256Hex(string input)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var entered = PasswordBox.Password ?? string.Empty;
            var enteredHash = ComputeSha256Hex(entered);
            var storedHash = ComputeSha256Hex(PlainPassword);
            if (string.Equals(enteredHash, storedHash, System.StringComparison.OrdinalIgnoreCase))
            {
                DialogResult = true;
                Close();
                return;
            }

            MessageBox.Show(this, "Incorrect password.", "Authentication Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            PasswordBox.Clear();
            PasswordBox.Focus();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToOverview();
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Treat window close (X) the same as Cancel button
            try
            {
                NavigateToOverview();
            }
            catch (System.Exception ex)
            {
                CopaFormGui.App.LogException("PasswordPrompt_OnWindowClosing", ex);
            }
            // allow close to proceed
        }

        private void NavigateToOverview()
        {
            var app = System.Windows.Application.Current;
            try
            {
                if (app != null)
                {
                    app.Dispatcher.Invoke(() =>
                    {
                        CopaFormGui.App.LogInfo("PasswordPrompt: navigating to Overview (Cancel/Close)");

                        // First, try resolving MainViewModel and OverviewViewModel from the DI container and set directly
                        var mainVmFromContainer = CopaFormGui.App.Services.GetService(typeof(CopaFormGui.ViewModels.MainViewModel)) as CopaFormGui.ViewModels.MainViewModel;
                        var overviewVm = CopaFormGui.App.Services.GetService(typeof(CopaFormGui.ViewModels.OverviewViewModel)) as CopaFormGui.ViewModels.OverviewViewModel;

                        if (mainVmFromContainer != null && overviewVm != null)
                        {
                            mainVmFromContainer.CurrentView = overviewVm;
                            mainVmFromContainer.CurrentViewName = "Overview";
                            return; // done
                        }

                        // If container resolution failed, fall back to MainWindow.DataContext
                        var mainWnd = app.MainWindow;
                        var mainVm = mainWnd?.DataContext as CopaFormGui.ViewModels.MainViewModel;
                        if (mainVm != null && overviewVm != null)
                        {
                            mainVm.CurrentView = overviewVm;
                            mainVm.CurrentViewName = "Overview";
                            return;
                        }

                        // Fallback to reflection-based attempt if direct assignment not possible
                        try
                        {
                            var main = mainWnd?.DataContext;
                            if (main != null)
                            {
                                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                                var prop = main.GetType().GetProperty("ShowOverviewCommand", flags);
                                if (prop != null)
                                {
                                    var cmd = prop.GetValue(main) as System.Windows.Input.ICommand;
                                    if (cmd != null && cmd.CanExecute(null)) { cmd.Execute(null); CopaFormGui.App.LogInfo("PasswordPrompt: ShowOverviewCommand executed"); return; }
                                }
                                var method = main.GetType().GetMethod("ShowOverview", flags, null, System.Type.EmptyTypes, null);
                                if (method != null) { method.Invoke(main, null); CopaFormGui.App.LogInfo("PasswordPrompt: ShowOverview() invoked via reflection"); return; }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            CopaFormGui.App.LogException("PasswordPrompt_NavigateFallback", ex);
                        }
                    });
                }
            }
            catch (System.Exception ex)
            {
                CopaFormGui.App.LogException("PasswordPrompt_Navigate", ex);
            }

            DialogResult = false;
            Close();
        }
    }
}

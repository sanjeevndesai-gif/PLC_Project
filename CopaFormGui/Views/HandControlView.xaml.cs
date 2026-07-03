namespace CopaFormGui.Views
{
    public partial class HandControlView : System.Windows.Controls.UserControl
    {
        private bool _authConfirmed = false;

        public HandControlView()
        {
            InitializeComponent();
            Loaded += HandControlView_Loaded;
        }

        private void HandControlView_Loaded(object? sender, System.Windows.RoutedEventArgs e)
        {
            if (_authConfirmed) return;
            var wnd = System.Windows.Window.GetWindow(this);
            var dlg = new PasswordPrompt { Owner = wnd };
            var ok = dlg.ShowDialog() ?? false;
            if (!ok)
            {
                CopaFormGui.App.LogInfo("HandControlView: auth cancelled — navigating back to Overview");
                // navigate main window back to Overview to avoid leaving a collapsed blank view
                try
                {
                    var app = System.Windows.Application.Current;
                    app?.Dispatcher.Invoke(() =>
                    {
                        var main = app.MainWindow?.DataContext;
                        if (main != null)
                        {
                            var prop = main.GetType().GetProperty("ShowOverviewCommand", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                            var cmd = prop?.GetValue(main) as System.Windows.Input.ICommand;
                            if (cmd != null && cmd.CanExecute(null)) { cmd.Execute(null); return; }
                            var method = main.GetType().GetMethod("ShowOverview", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                            method?.Invoke(main, null);
                        }
                    });
                }
                catch (System.Exception ex)
                {
                    CopaFormGui.App.LogException("HandControlView_Loaded", ex);
                }
                return;
            }
            else
            {
                _authConfirmed = true;
            }
        }

        private ViewModels.HandControlViewModel? Vm => DataContext as ViewModels.HandControlViewModel;

        private async void HomeXDownHandler(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Vm != null) await Vm.SetJogVariableAsync("X_HOME", 1);
        }

        private async void HomeXUpHandler(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Vm != null) await Vm.SetJogVariableAsync("X_HOME", 0);
        }

        private async void HomeYDownHandler(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Vm != null) await Vm.SetJogVariableAsync("Y_HOME", 1);
        }

        private async void HomeYUpHandler(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Vm != null) await Vm.SetJogVariableAsync("Y_HOME", 0);
        }

    private async void JogXPlusDownHandler(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Vm != null) await Vm.JogXPlusDown();
    }
    private async void JogXPlusUpHandler(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Vm != null) await Vm.JogXPlusUp();
    }
    private async void JogXMinusDownHandler(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Vm != null) await Vm.JogXMinusDown();
    }
    private async void JogXMinusUpHandler(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Vm != null) await Vm.JogXMinusUp();
    }
    private async void JogYPlusDownHandler(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Vm != null) await Vm.JogYPlusDown();
    }
    private async void JogYPlusUpHandler(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Vm != null) await Vm.JogYPlusUp();
    }
    private async void JogYMinusDownHandler(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Vm != null) await Vm.JogYMinusDown();
    }
    private async void JogYMinusUpHandler(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Vm != null) await Vm.JogYMinusUp();
    }
    private async void HomeXHandler(object sender, System.Windows.RoutedEventArgs e)
    {
        if (Vm != null) await Vm.HomeXAsync();
    }
    private async void HomeYHandler(object sender, System.Windows.RoutedEventArgs e)
    {
        if (Vm != null) await Vm.HomeYAsync();
    }
    private async void HomeAllDownHandler(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Vm != null) await Vm.SetJogVariableAsync("XY_HOME", 1);
    }
    private async void HomeAllUpHandler(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Vm != null) await Vm.SetJogVariableAsync("XY_HOME", 0);
    }
    private void HomeFeedrateChangedHandler(object sender, System.Windows.RoutedEventArgs e)
    {
        if (Vm != null && sender is System.Windows.Controls.TextBox tb)
        {
            Vm.HomeFeedrate = tb.Text;
        }
    }
}
}

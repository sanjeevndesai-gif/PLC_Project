namespace CopaFormGui.Views;

public partial class HandControlView : System.Windows.Controls.UserControl
{
    public HandControlView()
    {
        InitializeComponent();
    }

    private ViewModels.HandControlViewModel? Vm => DataContext as ViewModels.HandControlViewModel;

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

using System.Windows.Controls;

namespace CopaFormGui.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        this.Loaded += SettingsView_Loaded;
    }

    private void SettingsView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            CopaFormGui.App.LogInfo("SettingsView loaded successfully");
        }
        catch (Exception ex)
        {
            CopaFormGui.App.LogException("SettingsView.Loaded", ex);
        }
    }
}

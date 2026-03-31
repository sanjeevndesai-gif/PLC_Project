using System.Windows;

namespace CopaLicenseGenerator;

public partial class PasswordDialog : Window
{
    public string EnteredPassword => PasswordBox.Password;
    public PasswordDialog()
    {
        InitializeComponent();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

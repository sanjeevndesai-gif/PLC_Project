using System.Text;
using System.Windows;
using CopaFormGui.ViewModels;

namespace CopaFormGui;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Copa Form GUI\nVersion 1.0\n\nCNC Punching Machine Controller Interface\n\n© 2024 Copa Form",
            "About Copa Form",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}

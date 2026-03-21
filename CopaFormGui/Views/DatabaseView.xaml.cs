using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using CopaFormGui.Models;
using CopaFormGui.ViewModels;

namespace CopaFormGui.Views;

public partial class DatabaseView : UserControl
{
    public DatabaseView()
    {
        InitializeComponent();
    }

    private void RecordsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not DatabaseViewModel dbVm)
            return;

        PunchProgram? record = null;
        if (sender is DataGrid grid)
        {
            var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
            record = row?.Item as PunchProgram;
            if (record is null)
                record = grid.SelectedItem as PunchProgram;
        }

        record ??= dbVm.SelectedRecord;
        if (record is null)
            return;

        var mainVm = Window.GetWindow(this)?.DataContext as MainViewModel
            ?? App.Services.GetService<MainViewModel>();
        if (mainVm is null)
            return;

        mainVm.OpenPunchingRecord(record);
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}

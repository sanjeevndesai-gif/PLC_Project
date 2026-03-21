using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CopaFormGui.ViewModels;

namespace CopaFormGui.Views;

public partial class PunchingView : UserControl
{
    public PunchingView()
    {
        InitializeComponent();
    }

    private void StepsDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            RenumberSteps();
            RefreshPreviewShapes();
        }), DispatcherPriority.Background);
    }

    private void StepsDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        _ = sender;
        _ = e;
        Dispatcher.BeginInvoke(new Action(RefreshPreviewShapes), DispatcherPriority.Background);
    }

    private void StepsDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete) return;

        if (sender is DataGrid grid &&
            DataContext is PunchingViewModel vm &&
            grid.SelectedItem is CopaFormGui.Models.PunchStep selected)
        {
            vm.PunchSteps.Remove(selected);
            vm.SelectedStep = null;
            e.Handled = true;
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            RenumberSteps();
            RefreshPreviewShapes();
        }), DispatcherPriority.Background);
    }

    private void RenumberSteps()
    {
        if (DataContext is not PunchingViewModel vm) return;

        for (int i = 0; i < vm.PunchSteps.Count; i++)
            vm.PunchSteps[i].StepNumber = i + 1;
    }

    private void RefreshPreviewShapes()
    {
        if (DataContext is PunchingViewModel vm)
            vm.RefreshToolPreview();
    }
}

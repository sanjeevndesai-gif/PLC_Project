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

    // Allow only numbers and a single decimal point
    private void NumericTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        var textBox = sender as System.Windows.Controls.TextBox;
        string fullText = textBox?.Text.Remove(textBox?.SelectionStart ?? 0, textBox?.SelectionLength ?? 0) ?? string.Empty;
        fullText = fullText.Insert(textBox?.SelectionStart ?? 0, e.Text);
        e.Handled = !IsTextValidDecimal(fullText);
    }

    private bool IsTextValidDecimal(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        return System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d*(\.\d*)?$", System.Text.RegularExpressions.RegexOptions.Compiled);
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

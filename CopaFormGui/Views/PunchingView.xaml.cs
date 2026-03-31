// Ensure all using statements are at the top
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CopaFormGui.ViewModels;

namespace CopaFormGui.Views;

public partial class PunchingView : UserControl
{
    public PunchingView()
    {
        InitializeComponent();
        this.Loaded += PunchingView_Loaded;
    }

    private void PunchingView_Loaded(object sender, RoutedEventArgs e)
    {
        // Try to find the DataGrid by walking the visual tree
        var grid = FindDataGrid(this);
        if (grid != null)
        {
            grid.PreparingCellForEdit += DataGrid_PreparingCellForEdit;
            grid.BeginningEdit += DataGrid_BeginningEdit;
            grid.PreviewKeyDown += DataGrid_AdvancedKeyDown;
            grid.AddingNewItem += DataGrid_AddingNewItem;
            grid.MouseLeftButtonDown += DataGrid_MouseLeftButtonDown;
        }
    }

    private DataGrid? FindDataGrid(DependencyObject parent)
    {
        if (parent is DataGrid dg) return dg;
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            var result = FindDataGrid(child);
            if (result != null) return result;
        }
        return null;
    }

    private void DataGrid_PreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (e.EditingElement is TextBox tb)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }

    private void DataGrid_BeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        // Always enter edit mode on click
        if (e.Column.GetCellContent(e.Row) is TextBox tb)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }

    private void DataGrid_AdvancedKeyDown(object? sender, KeyEventArgs e)
    {
        var grid = sender as DataGrid;
        if (grid == null) return;

        if (e.Key == Key.Enter)
        {
            // Commit and move to next row, X cell
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            grid.CommitEdit(DataGridEditingUnit.Row, true);
            int row = grid.SelectedIndex + 1;
            if (row < grid.Items.Count)
            {
                grid.SelectedIndex = row;
                grid.CurrentCell = new DataGridCellInfo(grid.Items[row], grid.Columns[1]); // X column
                grid.BeginEdit();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            // Let default tab navigation work, but wrap to next row if at end
            var cell = grid.CurrentCell;
            int col = grid.Columns.IndexOf(cell.Column);
            int row = grid.SelectedIndex;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            if (!shift && col == grid.Columns.Count - 2) // Last editable col (F)
            {
                if (row < grid.Items.Count - 1)
                {
                    grid.SelectedIndex = row + 1;
                    grid.CurrentCell = new DataGridCellInfo(grid.Items[row + 1], grid.Columns[1]);
                    grid.BeginEdit();
                    e.Handled = true;
                }
            }
            else if (shift && col == 1) // First editable col (X)
            {
                if (row > 0)
                {
                    grid.SelectedIndex = row - 1;
                    grid.CurrentCell = new DataGridCellInfo(grid.Items[row - 1], grid.Columns[grid.Columns.Count - 2]);
                    grid.BeginEdit();
                    e.Handled = true;
                }
            }
        }
    }


    private void DataGrid_AddingNewItem(object? sender, AddingNewItemEventArgs e)
    {
        var grid = sender as DataGrid;
        if (grid == null) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            grid.SelectedIndex = grid.Items.Count - 1;
            grid.CurrentCell = new DataGridCellInfo(grid.Items[grid.Items.Count - 1], grid.Columns[1]);
            grid.BeginEdit();
        }), DispatcherPriority.Background);
    }

    private void DataGrid_MouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
    {
        var grid = sender as DataGrid;
        if (grid == null) return;
        var dep = (DependencyObject)e.OriginalSource;
        while (dep != null && !(dep is DataGridCell))
            dep = VisualTreeHelper.GetParent(dep);
        if (dep is DataGridCell cell)
        {
            grid.CurrentCell = new DataGridCellInfo(cell.DataContext, cell.Column);
            grid.BeginEdit();
        }
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

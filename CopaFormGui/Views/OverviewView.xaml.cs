using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CopaFormGui.ViewModels;

namespace CopaFormGui.Views;

public partial class OverviewView : System.Windows.Controls.UserControl
{
    // ...existing code...

    // Allow only numbers and a single decimal point
    private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var textBox = sender as System.Windows.Controls.TextBox;
        string fullText = textBox?.Text.Remove(textBox.SelectionStart, textBox.SelectionLength) ?? string.Empty;
        fullText = fullText.Insert(textBox?.SelectionStart ?? 0, e.Text);
        e.Handled = !IsTextValidDecimal(fullText);
    }

    private void NumericTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            string pasteText = (string)e.DataObject.GetData(typeof(string));
            var textBox = sender as System.Windows.Controls.TextBox;
            string fullText = textBox?.Text.Remove(textBox.SelectionStart, textBox.SelectionLength) ?? string.Empty;
            fullText = fullText.Insert(textBox?.SelectionStart ?? 0, pasteText);
            if (!IsTextValidDecimal(fullText))
                e.CancelCommand();
        }
        else
        {
            e.CancelCommand();
        }
    }

    private bool IsTextValidDecimal(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        return System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d*(\.\d*)?$");
    }
    private OverviewViewModel? _vm;
    // 3D preview fields removed

    public OverviewView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void RunButton_Click(object sender, RoutedEventArgs e)
    {
        RunPopup.IsOpen = true;
    }

    private void RunPopupOk_Click(object sender, RoutedEventArgs e)
    {
        RunPopup.IsOpen = false;

        // Get the ViewModel
        var vm = DataContext as CopaFormGui.ViewModels.OverviewViewModel;
        if (vm == null)
        {
            MessageBox.Show("ViewModel not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Try to get the latest punch program from the database
        var dataStoreServiceField = typeof(CopaFormGui.ViewModels.OverviewViewModel)
            .GetField("_dataStoreService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var dataStoreService = dataStoreServiceField?.GetValue(vm) as CopaFormGui.Services.IDataStoreService;
        if (dataStoreService == null)
        {
            MessageBox.Show("DataStoreService not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var programs = dataStoreService.LoadPunchPrograms();
        if (programs == null || programs.Count == 0)
        {
            MessageBox.Show("No punch programs found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Use the latest program with steps
        var latest = programs
            .Where(p => p.Steps != null && p.Steps.Count > 0)
            .OrderByDescending(p => p.ModifiedDate)
            .ThenByDescending(p => p.CreatedDate)
            .ThenByDescending(p => p.ProgramId)
            .FirstOrDefault();
        if (latest == null)
        {
            MessageBox.Show("No punch program with steps found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Build the file content
        var lines = new System.Collections.Generic.List<string>();
        lines.Add("OPEN PROG 99");
        int n = 1;
        foreach (var step in latest.Steps)
        {
            lines.Add($"N{n} X{step.X} Y{step.Y} F");
            n++;
        }
        lines.Add("CLOSE");

        // Use the program name from the database for the file name
        string safeName = string.IsNullOrWhiteSpace(latest.ProgramName) ? "punch_program" : latest.ProgramName;
        // Remove invalid filename characters
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            safeName = safeName.Replace(c, '_');
        string filePath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), safeName + ".txt");
        try
        {
            System.IO.File.WriteAllLines(filePath, lines);
            // Read the file content and set it to the ViewModel property
            string fileContent = System.IO.File.ReadAllText(filePath);
            vm.LastSavedFileContent = fileContent;
            MessageBox.Show($"Punch program written to:\n{filePath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Failed to write file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RunPopupCancel_Click(object sender, RoutedEventArgs e)
    {
        RunPopup.IsOpen = false;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachViewModel(DataContext as OverviewViewModel);
        // 3D preview removed
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.ToolPreviewShapes.CollectionChanged -= OnToolPreviewShapesChanged;
        }

        AttachViewModel(e.NewValue as OverviewViewModel);
        // 3D preview removed
    }

    private void AttachViewModel(OverviewViewModel? vm)
    {
        _vm = vm;
        if (_vm is null) return;
        _vm.PropertyChanged += OnVmPropertyChanged;
        _vm.ToolPreviewShapes.CollectionChanged += OnToolPreviewShapesChanged;
    }

    private void OnToolPreviewShapesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        // 3D preview removed
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OverviewViewModel.PreviewSheetLeft)
            or nameof(OverviewViewModel.PreviewSheetTop)
            or nameof(OverviewViewModel.PreviewSheetWidth)
            or nameof(OverviewViewModel.PreviewSheetHeight)
            or nameof(OverviewViewModel.ToolPreviewShapes))
        {
            if (e.PropertyName == nameof(OverviewViewModel.ToolPreviewShapes) && _vm is not null)
            {
                _vm.ToolPreviewShapes.CollectionChanged -= OnToolPreviewShapesChanged;
                _vm.ToolPreviewShapes.CollectionChanged += OnToolPreviewShapesChanged;
            }
            // 3D preview update removed
        }
    }

    // 3D preview methods removed

    // 3D preview methods removed
}

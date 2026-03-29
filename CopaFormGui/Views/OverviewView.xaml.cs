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
        MessageBox.Show("Run confirmed!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
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

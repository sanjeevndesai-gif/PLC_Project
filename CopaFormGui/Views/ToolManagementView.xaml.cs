using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;

namespace CopaFormGui.Views;

public partial class ToolManagementView : UserControl
{
    public ToolManagementView()
    {
        InitializeComponent();
    }

    private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var textBox = sender as TextBox;
        string fullText = textBox?.Text.Remove(textBox?.SelectionStart ?? 0, textBox?.SelectionLength ?? 0) ?? string.Empty;
        fullText = fullText.Insert(textBox?.SelectionStart ?? 0, e.Text);
        e.Handled = !IsTextValidDecimal(fullText);
    }

    private void NumericTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            string pasteText = (string)e.DataObject.GetData(typeof(string));
            var textBox = sender as TextBox;
            string fullText = textBox?.Text.Remove(textBox?.SelectionStart ?? 0, textBox?.SelectionLength ?? 0) ?? string.Empty;
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
        return System.Text.RegularExpressions.Regex.IsMatch(text, @"^\d*([\.,]\d*)?$", System.Text.RegularExpressions.RegexOptions.Compiled);
    }
}

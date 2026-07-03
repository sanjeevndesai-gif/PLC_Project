using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;

namespace CopaFormGui.Views;

public partial class SettingsView : UserControl
{
    private bool _authConfirmed = false;

    public SettingsView()
    {
        InitializeComponent();
        Loaded += SettingsView_Loaded;
    }

    private void SettingsView_Loaded(object? sender, RoutedEventArgs e)
    {
        if (_authConfirmed) return;
        var wnd = Window.GetWindow(this);
        var dlg = new PasswordPrompt { Owner = wnd };
        var ok = dlg.ShowDialog() ?? false;
        if (!ok)
        {
            CopaFormGui.App.LogInfo("SettingsView: auth cancelled — navigating back to Overview");
            try
            {
                var app = System.Windows.Application.Current;
                app?.Dispatcher.Invoke(() =>
                {
                    var main = app.MainWindow?.DataContext;
                    if (main != null)
                    {
                        var prop = main.GetType().GetProperty("ShowOverviewCommand", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                        var cmd = prop?.GetValue(main) as System.Windows.Input.ICommand;
                        if (cmd != null && cmd.CanExecute(null)) { cmd.Execute(null); return; }
                        var method = main.GetType().GetMethod("ShowOverview", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                        method?.Invoke(main, null);
                    }
                });
            }
            catch (System.Exception ex)
            {
                CopaFormGui.App.LogException("SettingsView_Loaded", ex);
            }
            return;
        }
        else
        {
            _authConfirmed = true;
        }
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
        // Allow optional leading negative sign, digits, optional decimal separator and digits
        return System.Text.RegularExpressions.Regex.IsMatch(text, @"^-?\d*([\.,]\d*)?$", System.Text.RegularExpressions.RegexOptions.Compiled);
    }
}
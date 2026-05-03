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
    private string? _lastGeneratedProgramPath;
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

        var toolRecords = dataStoreService.LoadToolRecords();
        var toolById = toolRecords.ToDictionary(t => t.ToolId, t => t);

        double feed = latest.Steps.FirstOrDefault(s => s.F > 0)?.F ?? 0;
        double autoFeed = new CopaFormGui.Services.SettingsService().LoadSettings().SpeedXHand;

        double xT1 = GetLastXForStation(latest, toolById, "T1", 50);
        double xT2 = GetLastXForStation(latest, toolById, "T2", 100);
        double xT4 = GetLastXForStation(latest, toolById, "T4", 150);
        double xT3 = GetLastXForStation(latest, toolById, "T3", xT2 + 4);

        double yT1 = GetLastYForStation(latest, toolById, "T1", 0);
        double yT2 = GetLastYForStation(latest, toolById, "T2", 0);
        double yT3 = GetLastYForStation(latest, toolById, "T3", 0);
        double yT4 = GetLastYForStation(latest, toolById, "T4", 0);

        string gT1 = GetGCodeForStation(toolRecords, "T1", "G54");
        string gT2 = GetGCodeForStation(toolRecords, "T2", "G55");
        string gT3 = GetGCodeForStation(toolRecords, "T3", "G56");
        string gT4 = GetGCodeForStation(toolRecords, "T4", "G57");

        int numberOfParts = Math.Max(0, vm.RunNumberOfParts);
        double widthForCuts = ParseMmFromText(vm.RecentWidthText, latest.Width);

        // Check which stations have steps saved in the program
        bool t1Used = latest.Steps.Any(s => toolById.TryGetValue(s.ToolId, out var tool) &&
            string.Equals(tool.ToolStation, "T1", StringComparison.OrdinalIgnoreCase));
        bool t2Used = latest.Steps.Any(s => toolById.TryGetValue(s.ToolId, out var tool) &&
            string.Equals(tool.ToolStation, "T2", StringComparison.OrdinalIgnoreCase));
        bool t3Used = latest.Steps.Any(s => toolById.TryGetValue(s.ToolId, out var tool) &&
            string.Equals(tool.ToolStation, "T3", StringComparison.OrdinalIgnoreCase));
        bool t4Used = latest.Steps.Any(s => toolById.TryGetValue(s.ToolId, out var tool) &&
            string.Equals(tool.ToolStation, "T4", StringComparison.OrdinalIgnoreCase));

        int nNum = 1;
        var lines = new List<string>
        {
            "//HEADER",
            "open prog 202",
            $"N{nNum++} G90",
            $"N{nNum++} linear",
            $"N{nNum++} G59",
            $"N{nNum++} M21",
            $"N{nNum++} M23",
            $"N{nNum++} M27",
            $"N{nNum++} X500 F{FormatNc(autoFeed)}",
            $"N{nNum++} Y910 F{FormatNc(autoFeed)}",
            $"N{nNum++} M28",
            "P2101=0",
            $"P2100={numberOfParts}",
            "while(P2101<P2100)",
            "{",
            $"N{nNum++} M26",
        };

        if (t1Used)
        {
            lines.Add("// FIRST TOOL POSITION");
            lines.Add($"N{nNum++} {gT1}");
            lines.Add($"N{nNum++} Y{FormatNc(yT1)}");
            lines.Add($"N{nNum++} X0");
            lines.Add($"N{nNum++} M22");
            lines.Add($"N{nNum++} M27");
            lines.Add($"N{nNum++} X{FormatNc(xT1)}");
            lines.Add($"N{nNum++} M26");
            lines.Add($"N{nNum++} M20");
            lines.Add($"N{nNum++} M21");
            if (vm.RunPartOff)
                lines.Add("M28");
        }

        if (t2Used)
        {
            lines.Add("// SECOND TOOL POSITION");
            lines.Add($"N{nNum++} {gT2}");
            lines.Add($"N{nNum++} Y{FormatNc(yT2)}");
            lines.Add($"N{nNum++} M27");
            lines.Add($"N{nNum++} X{FormatNc(xT2)}");
            lines.Add($"N{nNum++} M26");
            lines.Add($"N{nNum++} M20");
            lines.Add($"N{nNum++} M21");
        }

        if (t4Used)
        {
            lines.Add("// FOURTH TOOL POSITION");
            lines.Add($"N{nNum++} {gT4}");
            lines.Add($"N{nNum++} Y{FormatNc(yT4)}");
            lines.Add($"N{nNum++} M27");
            lines.Add($"N{nNum++} X{FormatNc(xT4)}");
            lines.Add($"N{nNum++} M26");
            lines.Add($"N{nNum++} M20");
            lines.Add($"N{nNum++} M21");
        }

        if (t3Used)
        {
            lines.Add("// THIRD TOOL POSITION");
            // Include cut n while previous cut's yOffset (10*(n-1)*n) < width
            // cut 1 always included; cut 4 stops when prev=120 >= width=100
            int cut = 1;
            while (cut == 1 || 10.0 * (cut - 1) * cut < widthForCuts)
            {
                double yOffset = 10.0 * cut * (cut + 1); // 20, 60, 120, 200, ...
                lines.Add($"// CUT {cut}");
                lines.Add($"N{nNum++} {gT3}");
                lines.Add($"N{nNum++} Y{FormatNc(yT3 + yOffset)}");
                lines.Add($"N{nNum++} M27");
                lines.Add($"N{nNum++} X{FormatNc(xT3 + 4)}");
                lines.Add($"N{nNum++} M26");
                lines.Add($"N{nNum++} M20");
                lines.Add($"N{nNum++} M21");
                cut++;
            }
            lines.Add($"N{nNum++} M27");
        }

        lines.AddRange(new[]
        {
            "// FOOTER",
            $"N{nNum++} X500",
            $"N{nNum++} M23",
            $"N{nNum++} M24",
            "DWELL 3000",
            $"N{nNum++} M25",
            "P2101=P2101+1",
            "}",
            $"N{nNum++} G59",
            $"N{nNum++} X500 Y910",
            $"N{nNum++} M30",
            "CLOSE"
        });

        lines = lines.Select(l => l.ToUpperInvariant()).ToList();

        // Use the program name from the database for the file name
        string safeName = string.IsNullOrWhiteSpace(latest.ProgramName) ? "punch_program" : latest.ProgramName;
        // Remove invalid filename characters
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            safeName = safeName.Replace(c, '_');
        string filePath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), safeName + ".txt");
        try
        {
            System.IO.File.WriteAllLines(filePath, lines);
            _lastGeneratedProgramPath = filePath;
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

    private void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select program file",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".txt"
        };

        if (dialog.ShowDialog() != true)
            return;

        string filePath = dialog.FileName;
        string fileContent;
        try
        {
            fileContent = System.IO.File.ReadAllText(filePath);
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Failed to read file: {ex.Message}", "Browse file", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _lastGeneratedProgramPath = filePath;

        var vm = DataContext as CopaFormGui.ViewModels.OverviewViewModel;
        if (vm != null)
            vm.LastSavedFileContent = fileContent;
    }

    private async void DownloadProgram_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;

        if (string.IsNullOrWhiteSpace(_lastGeneratedProgramPath) || !System.IO.File.Exists(_lastGeneratedProgramPath))
        {
            MessageBox.Show("Generate the program first using the RUN popup check button.", "Download program", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var vm = DataContext as CopaFormGui.ViewModels.OverviewViewModel;
        if (vm == null)
        {
            MessageBox.Show("ViewModel not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var controllerServiceField = typeof(CopaFormGui.ViewModels.OverviewViewModel)
            .GetField("_controllerService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var controllerService = controllerServiceField?.GetValue(vm) as CopaFormGui.Services.IControllerService;

        if (controllerService == null)
        {
            MessageBox.Show("Controller service not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (!controllerService.IsConnected)
        {
            MessageBox.Show("Controller is not connected.", "Download program", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Disable button and show progress bar to prevent multiple clicks
        DownloadProgramButton.IsEnabled = false;
        DownloadProgressBar.Visibility = System.Windows.Visibility.Visible;
        DownloadProgressText.Visibility = System.Windows.Visibility.Visible;

        bool success = false;
        try
        {
            success = await controllerService.DownloadSingleFileAsync(_lastGeneratedProgramPath!);
        }
        finally
        {
            DownloadProgressBar.Visibility = System.Windows.Visibility.Collapsed;
            DownloadProgressText.Visibility = System.Windows.Visibility.Collapsed;
            DownloadProgramButton.IsEnabled = true;
        }

        if (success)
        {
            MessageBox.Show("Program downloaded to PMAC successfully.", "Download program", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("Program download to PMAC failed.", "Download program", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static double GetLastXForStation(
        CopaFormGui.Models.PunchProgram program,
        Dictionary<int, CopaFormGui.Models.ToolRecord> toolById,
        string station,
        double fallback)
    {
        var step = program.Steps
            .Where(s => toolById.TryGetValue(s.ToolId, out var tool)
                        && string.Equals(tool.ToolStation, station, StringComparison.OrdinalIgnoreCase))
            .LastOrDefault();

        return step?.X ?? fallback;
    }

    private static double GetLastYForStation(
        CopaFormGui.Models.PunchProgram program,
        Dictionary<int, CopaFormGui.Models.ToolRecord> toolById,
        string station,
        double fallback)
    {
        var step = program.Steps
            .Where(s => toolById.TryGetValue(s.ToolId, out var tool)
                        && string.Equals(tool.ToolStation, station, StringComparison.OrdinalIgnoreCase))
            .LastOrDefault();

        return step?.Y ?? fallback;
    }

    private static string FormatNc(double value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static double ParseMmFromText(string? valueWithUnit, double fallback)
    {
        if (string.IsNullOrWhiteSpace(valueWithUnit))
            return fallback;

        string numeric = System.Text.RegularExpressions.Regex.Replace(valueWithUnit, "mm", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        if (double.TryParse(numeric, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        if (double.TryParse(numeric, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out parsed))
            return parsed;

        return fallback;
    }

    private static string GetGCodeForStation(
        List<CopaFormGui.Models.ToolRecord> toolRecords,
        string station,
        string fallback)
    {
        var raw = toolRecords
            .Where(t => string.Equals(t.ToolStation, station, StringComparison.OrdinalIgnoreCase))
            .Select(t => t.GCode)
            .LastOrDefault(code => !string.IsNullOrWhiteSpace(code));

        return NormalizeGCode(raw, fallback);
    }

    private static string NormalizeGCode(string? raw, string fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        var token = raw.Trim().ToUpperInvariant();
        if (token.StartsWith("G"))
            return token;

        if (double.TryParse(token, out _))
            return $"G{token}";

        return token;
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

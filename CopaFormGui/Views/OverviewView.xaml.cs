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

        var settings = new CopaFormGui.Services.SettingsService().LoadSettings();
        var toolRecords = dataStoreService.LoadToolRecords();
        var toolById = toolRecords.ToDictionary(t => t.ToolId, t => t);

        double feed = latest.Steps.FirstOrDefault(s => s.F > 0)?.F ?? 0;

        double xT1 = GetLastXForStation(latest, toolById, "T1", 50);
        double xT2 = GetLastXForStation(latest, toolById, "T2", 100);
        double xT4 = GetLastXForStation(latest, toolById, "T4", 150);
        double xT3 = GetLastXForStation(latest, toolById, "T3", xT2 + 4);

        double yT1 = settings.T1OffsetPos;
        double yT2 = settings.T2OffsetPos;
        double yT3 = settings.T3OffsetPos;
        double yT4 = settings.T4OffsetPos;

        string gT1 = GetGCodeForStation(toolRecords, "T1", "G54");
        string gT2 = GetGCodeForStation(toolRecords, "T2", "G55");
        string gT3 = GetGCodeForStation(toolRecords, "T3", "G56");
        string gT4 = GetGCodeForStation(toolRecords, "T4", "G57");

        int numberOfParts = Math.Max(0, vm.RunNumberOfParts);

        var lines = new List<string>
        {
            "//HEADER",
            "open prog 202",
            "N1 G90",
            "N2 linear",
            "N3 G59",
            "N4 M21",
            "N5 M23",
            "N6 M27",
            $"N7 X500 F{FormatNc(feed)}",
            $"N8 Y910 F{FormatNc(feed)}",
            "N9 M28",
            "P2101=0",
            "while(P2101<P2100)",
            "{",
            "N10 M26",
            "// FIRST TOOL POSITION",
            $"N11 {gT1}",
            $"N12 Y{FormatNc(yT1)}",
            "N13 X0",
            "N14 M22",
            "N15 M27",
            $"N16 X{FormatNc(xT1)}",
            "N17 M26",
            "N18 M20",
            "N19 M21"
        };

        if (vm.RunPartOff)
            lines.Add("M28");

        lines.AddRange(new[]
        {
            "// SECOND TOOL POSITION",
            $"N20 {gT2}",
            $"N21 Y{FormatNc(yT2)}",
            "N22 M27",
            $"N23 X{FormatNc(xT2)}",
            "N24 M26",
            "N25 M20",
            "N26 M21",
            "// FOURTH TOOL POSITION",
            $"N28 {gT4}",
            $"N29 Y{FormatNc(yT4)}",
            "N30 M27",
            $"N31 X{FormatNc(xT4)}",
            "N32 M26",
            "N33 M20",
            "N34 M21",
            "// THIRD TOOL POSITION",
            "// FIRST CUT",
            $"N35 {gT3}",
            $"N36 Y{FormatNc(yT3)}",
            "N37 M27",
            $"N38 X{FormatNc(xT3 + 4)}",
            "N39 M26",
            "N40 M20",
            "N41 M21",
            "// SECOND CUT",
            $"N43 {gT3}",
            $"N44 Y{FormatNc(yT3 + 40)}",
            $"N45 X{FormatNc(xT3 + 4)}",
            "N46 M26",
            "N47 M20",
            "N48 M21",
            "N49 M27",
            "// FOOTER",
            "N50 X500",
            "N51 M23",
            "N52 M24",
            "DWELL 3000",
            "N53 M25",
            "P2101=P2101+1",
            "}",
            "N54 G59",
            "N55 X500 Y910",
            "N56 M30",
            "CLOSE"
        });

        lines.Insert(lines.IndexOf("P2101=0") + 1, $"P2100={numberOfParts}");
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

        var success = await controllerService.DownloadSingleFileAsync(_lastGeneratedProgramPath!);
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

    private static string FormatNc(double value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
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

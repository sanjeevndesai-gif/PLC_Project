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

    private void SetupLastSavedHeaderResizer()
    {
        if (LastSavedHeaderGrid == null || LastSavedLabelTextBlock == null || ExpandSavedButton == null)
            return;

        LastSavedHeaderGrid.SizeChanged += (s, e) => UpdateLastSavedMaxWidth();
        ExpandSavedButton.SizeChanged += (s, e) => UpdateLastSavedMaxWidth();
        UpdateLastSavedMaxWidth();
    }

    private void UpdateLastSavedMaxWidth()
    {
        try
        {
            double parentWidth = LastSavedHeaderGrid.ActualWidth;
            double btnTotal = ExpandSavedButton.ActualWidth + ExpandSavedButton.Margin.Left + ExpandSavedButton.Margin.Right;
            double pad = 8.0; // small gap between text and button
            double max = Math.Max(20.0, parentWidth - btnTotal - pad);
            LastSavedLabelTextBlock.MaxWidth = max;
        }
        catch
        {
            // ignore measurement errors
        }
    }

    // Font size and expand/collapse for Last Saved File content
    private double _lastSavedFileFontSize = 12.0;
    private const double _lastSavedCollapsedHeight = 120.0;
    private const double _lastSavedExpandedHeight = 400.0;

    private void IncreaseSavedFileFont_Click(object sender, RoutedEventArgs e)
    {
        _lastSavedFileFontSize = Math.Min(28.0, _lastSavedFileFontSize + 1.0);
        if (LastSavedTextBox != null) LastSavedTextBox.FontSize = _lastSavedFileFontSize;
    }

    private void DecreaseSavedFileFont_Click(object sender, RoutedEventArgs e)
    {
        _lastSavedFileFontSize = Math.Max(8.0, _lastSavedFileFontSize - 1.0);
        if (LastSavedTextBox != null) LastSavedTextBox.FontSize = _lastSavedFileFontSize;
    }

    private void ExpandSavedFile_Checked(object sender, RoutedEventArgs e)
    {
        if (LastSavedScrollViewer != null) LastSavedScrollViewer.Height = _lastSavedExpandedHeight;
    }

    private void ExpandSavedFile_Unchecked(object sender, RoutedEventArgs e)
    {
        if (LastSavedScrollViewer != null) LastSavedScrollViewer.Height = _lastSavedCollapsedHeight;
    }

    // Label size is fixed/styled in XAML now.

    private void RunButton_Click(object sender, RoutedEventArgs e)
    {
        RunPopup.IsOpen = true;
    }

    /// <summary>
    /// Handles the OK button click in the Run popup.
    /// Generates a PMAC CNC punch program (NC code) from the latest saved punch program
    /// in the database and writes it to a .txt file in the user's Documents folder.
    /// The program loops for the configured number of parts.
    /// </summary>
    private void RunPopupOk_Click(object sender, RoutedEventArgs e)
    {
        // ── Close the Run popup ───────────────────────────────────────────────
        RunPopup.IsOpen = false;

        // ── Resolve ViewModel ─────────────────────────────────────────────────
        // The ViewModel holds UI-bound properties such as RunNumberOfParts and RecentWidthText.
        var vm = DataContext as CopaFormGui.ViewModels.OverviewViewModel;
        if (vm == null)
        {
            MessageBox.Show("ViewModel not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // ── Resolve DataStoreService via reflection ───────────────────────────
        // The service is a private field on the ViewModel; reflection is used
        // because the View does not hold a direct reference to it.
        var dataStoreServiceField = typeof(CopaFormGui.ViewModels.OverviewViewModel)
            .GetField("_dataStoreService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var dataStoreService = dataStoreServiceField?.GetValue(vm) as CopaFormGui.Services.IDataStoreService;
        if (dataStoreService == null)
        {
            MessageBox.Show("DataStoreService not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // ── Load punch programs from the database ─────────────────────────────
        var programs = dataStoreService.LoadPunchPrograms();
        if (programs == null || programs.Count == 0)
        {
            MessageBox.Show("No punch programs found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // ── Select the most recently modified program that has steps ──────────
        // Tie-break by CreatedDate, then by ProgramId (highest = newest).
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

        // ── Load tool records and build a fast lookup dictionary ──────────────
        // toolById[toolId] → ToolRecord (contains ToolStation: T1/T2/T3/T4 and GCode)
        var toolRecords = dataStoreService.LoadToolRecords();
        var toolById = toolRecords.ToDictionary(t => t.ToolId, t => t);

        // ── Resolve feed rates ────────────────────────────────────────────────
        // feed     : the F-value from the first step that specifies one (not used in output currently)
        // autoFeed : the machine's X-hand speed from Settings, used in the HEADER moves
        double feed = latest.Steps.FirstOrDefault(s => s.F > 0)?.F ?? 0;
        double autoFeed = new CopaFormGui.Services.SettingsService().LoadSettings().SpeedXHand;

        // ── Partition steps by tool station ───────────────────────────────────
        // Each station maps to a physical punch position on the machine:
        //   T1 = First punch station  (G54)
        //   T2 = Second punch station (G55)
        //   T3 = Cut-off station      (G56) — always runs last
        //   T4 = Fourth punch station (G57)
        var t1Steps = latest.Steps
            .Where(s => toolById.TryGetValue(s.ToolId, out var t) && string.Equals(t.ToolStation, "T1", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var t2Steps = latest.Steps
            .Where(s => toolById.TryGetValue(s.ToolId, out var t) && string.Equals(t.ToolStation, "T2", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var t3Steps = latest.Steps
            .Where(s => toolById.TryGetValue(s.ToolId, out var t) && string.Equals(t.ToolStation, "T3", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var t4Steps = latest.Steps
            .Where(s => toolById.TryGetValue(s.ToolId, out var t) && string.Equals(t.ToolStation, "T4", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // ── Resolve G-code work offsets per station ───────────────────────────
        // Reads the GCode field from the tool record; falls back to the default if missing.
        string gT1 = GetGCodeForStation(toolRecords, "T1", "G54");
        string gT2 = GetGCodeForStation(toolRecords, "T2", "G55");
        string gT3 = GetGCodeForStation(toolRecords, "T3", "G56");
        string gT4 = GetGCodeForStation(toolRecords, "T4", "G57");

        // ── Runtime parameters ────────────────────────────────────────────────
        // numberOfParts : how many times the while-loop runs (set by the operator in the UI)
        // widthForCuts  : part width in mm, used to decide how many cut passes T3 makes
        int numberOfParts = Math.Max(0, vm.RunNumberOfParts);
        double widthForCuts = ParseMmFromText(vm.RecentWidthText, latest.Width);

        // Flags — true when at least one step exists for that station
        bool t1Used = t1Steps.Any();
        bool t2Used = t2Steps.Any();
        bool t3Used = t3Steps.Any();
        bool t4Used = t4Steps.Any();

        // ── Build the NC program line by line ─────────────────────────────────
        // nNum is the running N-number counter incremented for every motion/M-code line.
        int nNum = 1;

        // ── HEADER ────────────────────────────────────────────────────────────
        // Opens program slot 202, sets absolute mode (G90), linear interpolation,
        // homes to G59, releases clamp (M21), engages brake (M23), parks the axes,
        // then initialises the part counter before entering the production loop.
            var lines = new List<string>
        {
            "//HEADER",
            "open prog 202",          // Open PMAC program buffer 202
            $"N{nNum++} G90",         // Absolute positioning mode
            $"N{nNum++} linear",      // Linear interpolation mode
            $"N{nNum++} G59",         // Select home/park coordinate system
            $"N{nNum++} M21",         // Release clamp
            $"N{nNum++} M23",         // Engage brake
            $"N{nNum++} M27",         // Retract punch head
            $"N{nNum++} X500 F{FormatNc(autoFeed)}",   // Move X to park position
            $"N{nNum++} Y910 F{FormatNc(autoFeed)}",   // Move Y to park position
            $"N{nNum++} M28",         // Part-off / eject
            "P2101=0",                // Reset part counter
            $"P2100={numberOfParts}", // Set total parts target
            "while(P2101<P2100)",     // Loop until all parts are produced
            "{",
            $"N{nNum++} M26",         // Lower punch / engage cycle start
        };

        // ── PUNCH STEPS (T1, T2, T4) — in database order ─────────────────────
        // T3 (cut-off) is intentionally excluded here and always appended last.
        // Steps are emitted one-by-one in the exact order they appear in the database,
        // so the machine follows the operator-defined punch sequence.
        //
        // The very first step of any station receives an X0 retract + M22 clamp command
        // before the punch move — this homes the X-axis and locks the clamp for safety.
        // All subsequent steps go directly to the punch sequence.
        var nonCutSteps = latest.Steps
            .Where(s => toolById.TryGetValue(s.ToolId, out var t) &&
                        !string.Equals(t.ToolStation, "T3", StringComparison.OrdinalIgnoreCase))
            .ToList();

        string? prevStation = null; // Tracks station changes to insert position comment headers
        bool isFirstStep = true;    // True only for the very first punch step overall

        foreach (var step in nonCutSteps)
        {
            if (!toolById.TryGetValue(step.ToolId, out var tool)) continue;
            string stationUpper = tool.ToolStation?.ToUpperInvariant() ?? "";

            // Insert a position comment whenever the station changes
            if (stationUpper != prevStation)
            {
                lines.Add($"// {stationUpper} POSITION");
                prevStation = stationUpper;
            }

            // Select the work-offset G-code for this station
            string gCode = stationUpper switch
            {
                "T1" => gT1,
                "T2" => gT2,
                "T4" => gT4,
                _ => ""
            };

            if (string.IsNullOrEmpty(gCode)) continue;

            // Select work offset and position Y
            lines.Add($"N{nNum++} {gCode}");          // Activate station work offset
            lines.Add($"N{nNum++} Y{FormatNc(step.Y)}"); // Move Y to punch position

            if (isFirstStep)
            {
                // First step only: retract X to home and engage clamp before punching
                lines.Add($"N{nNum++} G59"); 
                lines.Add($"N{nNum++} DWELL 10"); 
                lines.Add($"N{nNum++} X0");   // Retract X axis to home
                lines.Add($"N{nNum++} M22");  // Engage clamp
            }

            // Punch sequence: extend head → move X → punch down → punch up → retract
            lines.Add($"N{nNum++} M27");  
            if (isFirstStep)            // Extend punch head
            {
                lines.Add($"N{nNum++} {gCode}"); 
                lines.Add($"N{nNum++} DWELL 10"); 
            }

            // Mark that the special first-step handling has been emitted
            isFirstStep = false;
            lines.Add($"N{nNum++} X{FormatNc(step.X)}"); // Move X to punch X coordinate
            lines.Add($"N{nNum++} M26");               // Punch down (activate punch)
            lines.Add($"N{nNum++} M20");               // Punch cycle step 1
            lines.Add($"N{nNum++} M21");               // Punch cycle step 2 / retract
            

            // Optional part-off signal after each T1 punch (if operator enabled it)
            if (stationUpper == "T1" && vm.RunPartOff)
                lines.Add("M28");
        }

        // ── T3 CUT-OFF — always last ──────────────────────────────────────────
        // T3 is the cut-off station and must always run after all punching is done.
        // Number of cut passes depends on part width:
        //   width ≤ 80 mm → 2 passes at Y+20, Y+60
        //   width  > 80 mm → 3 passes at Y+20, Y+60, Y+100
        // X is offset by +4 mm to account for the cut blade geometry.
        if (t3Used)
        {
            lines.Add("// T3 POSITION");

            // Determine number of cut passes based on part width (mm):
            //  <40   => 1 pass
            //  40-80 => 2 passes
            //  80-120=> 3 passes
            // 120-160=> 4 passes (max)
            double clampedWidth = Math.Max(0.0, Math.Min(160.0, widthForCuts));
            int passes;
            if (clampedWidth <= 40.0) passes = 1;
            else if (clampedWidth <= 80.0) passes = 2;
            else if (clampedWidth <= 120.0) passes = 3;
            else passes = 4;

            // Offsets into the material: start at +20 mm, then every +40 mm for each additional pass
            double[] cutOffsets = Enumerable.Range(0, passes).Select(i => 20.0 + i * 40.0).ToArray();

            foreach (var step in t3Steps)
            {
                int cutNum = 1;
                foreach (var yOffset in cutOffsets)
                {
                    lines.Add($"// CUT {cutNum}");
                    // Special sequence when this program uses only a single tool and this is the first cut
                    // (emits a home/retract/clamp cycle before performing the cut)
                    var distinctToolCount = latest.Steps.Select(s => s.ToolId).Distinct().Count();
                    bool singleToolProgram = distinctToolCount == 1;

                    if (cutNum == 1 && singleToolProgram)
                    {
                        lines.Add($"N{nNum++} {gT3}");                        // Activate T3 work offset
                        lines.Add($"N{nNum++} Y{FormatNc(step.Y + yOffset)}"); // Y cut position (offset into material)
                        lines.Add($"N{nNum++} G59");
                        lines.Add($"N{nNum++} DWELL 10");
                        lines.Add($"N{nNum++} X0");
                        lines.Add($"N{nNum++} M22");
                        lines.Add($"N{nNum++} M27");
                        lines.Add($"N{nNum++} {gT3}");                          // Extend cut head
                        lines.Add($"N{nNum++} X{FormatNc(step.X + 4)}");      // X cut position (+4 mm blade offset)
                        lines.Add($"N{nNum++} M26");                           // Cut down
                        lines.Add($"N{nNum++} M20");                           // Cut cycle step 1
                        lines.Add($"N{nNum++} M21");
                    }
                   else if (cutNum == 1)
                    {
                        lines.Add($"N{nNum++} {gT3}");                        // Activate T3 work offset
                        lines.Add($"N{nNum++} Y{FormatNc(step.Y + yOffset)}"); // Y cut position (offset into material)
                        lines.Add($"N{nNum++} G59");
                        lines.Add($"N{nNum++} DWELL 10");
                        lines.Add($"N{nNum++} X0");
                        lines.Add($"N{nNum++} M22");
                        lines.Add($"N{nNum++} M27");
                        lines.Add($"N{nNum++} {gT3}");                          // Extend cut head
                        lines.Add($"N{nNum++} X{FormatNc(step.X + 4)}");      // X cut position (+4 mm blade offset)
                        lines.Add($"N{nNum++} M26");                           // Cut down
                        lines.Add($"N{nNum++} M20");                           // Cut cycle step 1
                        lines.Add($"N{nNum++} M21");
                    }
                    else
                    {
                        lines.Add($"N{nNum++} {gT3}");                        // Activate T3 work offset
                        lines.Add($"N{nNum++} Y{FormatNc(step.Y + yOffset)}"); // Y cut position (offset into material)
                       // lines.Add($"N{nNum++} M27");                           // Extend cut head
                        lines.Add($"N{nNum++} X{FormatNc(step.X + 4)}");      // X cut position (+4 mm blade offset)
                        lines.Add($"N{nNum++} M26");                           // Cut down
                        lines.Add($"N{nNum++} M20");                           // Cut cycle step 1
                        lines.Add($"N{nNum++} M21");                           // Cut cycle step 2 / retract
                    }
                    cutNum++;
                }
            }
          //  lines.Add($"N{nNum++} M27"); // Final retract after all cuts
        }

        // ── FOOTER ────────────────────────────────────────────────────────────
        // Returns the machine to the park position, triggers end-of-cycle M-codes,
        // dwells 3 seconds (for part ejection / operator clearance), increments
        // the part counter, then closes the loop. After all parts are done,
        // the machine homes and ends the program.
        // Compute last T3 X once and reuse for footer moves (offset +100mm)
        double lastXForT3 = GetLastXForStation(latest, toolById, "T3", 500.0);

        // FOOTER
        lines.Add("// FOOTER");
        lines.Add($"N{nNum++} X{FormatNc(lastXForT3 + 100)}");        // Return X to park position (T3 last X +100)
        lines.Add($"N{nNum++} M23");         // Engage brake
        lines.Add($"N{nNum++} M24");         // End-of-part signal
        lines.Add("DWELL 3000");             // Wait 3 seconds (part eject / clearance)
        lines.Add($"N{nNum++} M25");         // Release end-of-part signal
        lines.Add("P2101=P2101+1");          // Increment part counter
        // Optionally add part-off signal if operator enabled it via RunPartOff
        if (vm.RunPartOff)
        {
            lines.Add($"N{nNum++} M28");     // Part-off signal
        }
        lines.Add("}");                              // End of while-loop
        lines.Add($"N{nNum++} G59");         // Restore home coordinate system
        lines.Add($"N{nNum++} X0"); 
        lines.Add($"N{nNum++} M22"); 
        lines.Add($"N{nNum++} M27"); 
        lines.Add($"N{nNum++} X{FormatNc(lastXForT3 + 100)}");
        lines.Add($"N{nNum++} M23"); 
        lines.Add($"N{nNum++} M24"); 
        lines.Add($"N{nNum++} DWELL 3000"); 
        lines.Add($"N{nNum++} M25"); 
        lines.Add($"N{nNum++} Y910"); 
        lines.Add($"N{nNum++} M30"); 

       // lines.Add($"N{nNum++} G59");         // Restore home coordinate system
       // lines.Add($"N{nNum++} X500 Y910");   // Park axes
       // lines.Add($"N{nNum++} M30");         // End of program
        lines.Add("CLOSE");                  // Close PMAC program buffer

        // ── Convert all lines to UPPER CASE (PMAC requirement) ────────────────
        lines = lines.Select(l => l.ToUpperInvariant()).ToList();

        // ── Write program to file ─────────────────────────────────────────────
        // File name = sanitised program name from the database, saved to Documents.
        string safeName = string.IsNullOrWhiteSpace(latest.ProgramName) ? "punch_program" : latest.ProgramName;
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            safeName = safeName.Replace(c, '_');
        string filePath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), safeName + ".txt");
        try
        {
            System.IO.File.WriteAllLines(filePath, lines);
            _lastGeneratedProgramPath = filePath;

            // Push file content to the ViewModel so the UI preview updates
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
        SetupLastSavedHeaderResizer();
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

    private void ExpandSavedFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LastSavedPopup.IsOpen = true;
        }
        catch
        {
            // ignore
        }
    }

    private void CloseLastSavedPopup_Click(object sender, RoutedEventArgs e)
    {
        LastSavedPopup.IsOpen = false;
    }
}

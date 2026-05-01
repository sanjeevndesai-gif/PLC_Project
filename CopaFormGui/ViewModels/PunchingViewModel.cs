using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Models;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;

public enum MachineMode { Manual, Auto, Homing }

public partial class PunchingViewModel : ObservableObject
{
    // Helper: Update ToolInfo for all PunchSteps
    private void UpdateAllPunchStepToolInfo()
    {
        var toolLookup = UsedTools.ToDictionary(t => t.ToolId);
        foreach (var step in PunchSteps)
        {
            UpdatePunchStepToolInfo(step, toolLookup);
        }
    }

    private void UpdatePunchStepToolInfo(PunchStep step, Dictionary<int, ToolRecord>? toolLookup = null)
    {
        toolLookup ??= UsedTools.ToDictionary(t => t.ToolId);
        if (toolLookup.TryGetValue(step.ToolId, out var tool))
        {
            step.ToolStation = string.IsNullOrWhiteSpace(tool.ToolStation) ? $"T{tool.ToolId}" : tool.ToolStation;
            // Show only Dia if L/W are zero, only L/W if Dia is zero, else show all
            bool hasDia = tool.Diameter > 0;
            bool hasL = tool.Length > 0;
            bool hasW = tool.Width > 0;
            if (hasDia && !hasL && !hasW)
                step.ToolInfo = $"Dia {tool.Diameter}";
            else if (!hasDia && hasL && hasW)
                step.ToolInfo = $"L = {tool.Length} w = {tool.Width}";
            else if (hasDia && (hasL || hasW))
                step.ToolInfo = $"Dia {tool.Diameter} L = {tool.Length} w = {tool.Width}";
            else
                step.ToolInfo = string.Empty;
        }
        else
        {
            step.ToolStation = string.Empty;
            step.ToolInfo = string.Empty;
        }
    }

    private void SubscribePunchStepPropertyChanged(PunchStep step)
    {
        step.PropertyChanged -= PunchStep_PropertyChanged;
        step.PropertyChanged += PunchStep_PropertyChanged;
    }

    private void UnsubscribePunchStepPropertyChanged(PunchStep step)
    {
        step.PropertyChanged -= PunchStep_PropertyChanged;
    }

    private void PunchStep_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is PunchStep step && e.PropertyName == nameof(PunchStep.ToolId))
        {
            UpdatePunchStepToolInfo(step);
        }
    }
    private ObservableCollection<ToolRecord> _usedTools = new();
    public ObservableCollection<ToolRecord> UsedTools
    {
        get => _usedTools;
        set => SetProperty(ref _usedTools, value);
    }

    public void RefreshUsedTools()
    {
        var allTools = _dataStoreService.LoadToolRecords();
        var used = allTools.Where(t => t.IsUsed).ToList();
        UsedTools = new ObservableCollection<ToolRecord>(used);
        UpdateAllPunchStepToolInfo();
    }

    [RelayCommand]
    private void RefreshUsedToolsCommand()
    {
        RefreshUsedTools();
    }
    private readonly IControllerService _controllerService;
    private readonly IDataStoreService _dataStoreService;

    [ObservableProperty] private bool _isConnected;

    // Axis positions
    [ObservableProperty] private double _posX;
    [ObservableProperty] private double _posY;

    // Target positions
    [ObservableProperty] private double _targetX;
    [ObservableProperty] private double _targetY;

    [ObservableProperty] private MachineMode _currentMode = MachineMode.Manual;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isEmergencyStop;

    [ObservableProperty] private string _programName = string.Empty;
    [ObservableProperty] private int _totalStrokes;
    [ObservableProperty] private int _completedStrokes;

    [ObservableProperty]
    private ObservableCollection<PunchProgram> _programs = new();

    [ObservableProperty]
    private PunchProgram? _selectedProgram;

    [ObservableProperty]
    private PunchStep? _selectedStep;

    [ObservableProperty]
    private ObservableCollection<PunchStep> _punchSteps = new();

    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private string _material = "CU";
    [ObservableProperty] private string _comment = string.Empty;
    [ObservableProperty] private string _referenceBending = string.Empty;
    [ObservableProperty] private string _length = "106.800";
    [ObservableProperty] private string _width = "100.000";
    [ObservableProperty] private string _thickness = "5.000";
    [ObservableProperty] private double _previewBoxWidth = 150;
    [ObservableProperty] private double _previewBoxHeight = 140;
    [ObservableProperty] private double _previewDepthOffsetX = 8;
    [ObservableProperty] private double _previewDepthOffsetY = -8;
    [ObservableProperty] private string _previewDimensionText = "L: 106.800  W: 100.000  T: 5.000";
    [ObservableProperty] private double _previewFrontLeft = 85;
    [ObservableProperty] private double _previewFrontTop = 35;
    [ObservableProperty] private double _previewBackLeft = 93;
    [ObservableProperty] private double _previewBackTop = 27;
    [ObservableProperty] private double _previewFrontRight = 235;
    [ObservableProperty] private double _previewFrontBottom = 175;
    [ObservableProperty] private double _previewBackRight = 243;
    [ObservableProperty] private double _previewBackBottom = 167;

    [ObservableProperty] private double _lengthArrowY = 188;
    [ObservableProperty] private double _lengthLabelX = 140;
    [ObservableProperty] private double _lengthLabelY = 191;
    [ObservableProperty] private string _lengthLabelText = "L = 106.800";

    [ObservableProperty] private double _widthArrowX = 68;
    [ObservableProperty] private double _widthLabelX = 20;
    [ObservableProperty] private double _widthLabelY = 102;
    [ObservableProperty] private string _widthLabelText = "W = 100.000";

    [ObservableProperty] private double _thicknessLabelX = 248;
    [ObservableProperty] private double _thicknessLabelY = 18;
    [ObservableProperty] private string _thicknessLabelText = "T = 5.000";
    [ObservableProperty] private ObservableCollection<PunchPreviewShape> _toolPreviewShapes = new();
    [ObservableProperty] private double _previewZoom = 1.0;

    [ObservableProperty] private int _selectedToolId = 1;

    private System.Timers.Timer? _positionUpdateTimer;

    public PunchingViewModel(IControllerService controllerService, IDataStoreService dataStoreService)
    {
        _controllerService = controllerService;
        _dataStoreService = dataStoreService;
        _controllerService.ConnectionStateChanged += OnConnectionStateChanged;
        IsConnected = controllerService.IsConnected;
        PunchSteps.CollectionChanged += OnPunchStepsCollectionChanged;
        foreach (var step in PunchSteps)
            SubscribePunchStepPropertyChanged(step);
        // Subscribe to tool list changes
        DataStoreService.ToolListChanged += RefreshUsedTools;
        // Initialize UsedTools
        RefreshUsedTools();
        LoadSamplePrograms();
        ClearEditorState();
        UpdateDimensionPreview();
        RefreshToolPreview();
    }

    partial void OnSelectedProgramChanged(PunchProgram? value)
    {
        PunchSteps.CollectionChanged -= OnPunchStepsCollectionChanged;
        foreach (var step in PunchSteps)
            UnsubscribePunchStepPropertyChanged(step);
        PunchSteps = value is null
            ? new ObservableCollection<PunchStep>()
            : new ObservableCollection<PunchStep>(value.Steps);
        PunchSteps.CollectionChanged += OnPunchStepsCollectionChanged;
        foreach (var step in PunchSteps)
            SubscribePunchStepPropertyChanged(step);

        if (value is not null)
        {
            ProgramName = value.ProgramName;
            Material = value.Material;
            Comment = value.Comment;
            ReferenceBending = value.ReferenceBending;
            Length = FormatValue(value.Length);
            Width = FormatValue(value.Width);
            Thickness = FormatValue(value.Thickness);
        }

        SelectedStep = PunchSteps.FirstOrDefault();
        UpdateDimensionPreview();
        RefreshToolPreview();
        // Ensure ToolInfo is updated for all steps when loading from database
        UpdateAllPunchStepToolInfo();
    }

    partial void OnSelectedStepChanged(PunchStep? value)
    {
        _ = value;
        RefreshToolPreview();
    }

    private void OnPunchStepsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e is not null)
        {
            if (e.OldItems != null)
            {
                foreach (PunchStep oldStep in e.OldItems)
                    UnsubscribePunchStepPropertyChanged(oldStep);
            }
            if (e.NewItems != null)
            {
                foreach (PunchStep newStep in e.NewItems)
                {
                    SubscribePunchStepPropertyChanged(newStep);
                    UpdatePunchStepToolInfo(newStep);
                }
            }
        }
        RenumberPunchSteps();
        SyncStepsToSelectedProgram();
        RefreshToolPreview();
        UpdateAllPunchStepToolInfo();
    }

    private void RenumberPunchSteps()
    {
        for (int i = 0; i < PunchSteps.Count; i++)
        {
            var expected = i + 1;
            if (PunchSteps[i].StepNumber != expected)
                PunchSteps[i].StepNumber = expected;
        }
    }

    private void SyncStepsToSelectedProgram()
    {
        if (SelectedProgram is null) return;
        SelectedProgram.Steps = PunchSteps.ToList();
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        IsConnected = state == ConnectionState.Connected;
        if (IsConnected)
            StartPositionPolling();
        else
            StopPositionPolling();
    }

    private void LoadSamplePrograms()
    {
        var storedPrograms = _dataStoreService.LoadPunchPrograms();
        Programs = new ObservableCollection<PunchProgram>(storedPrograms);
    }

    private void StartPositionPolling()
    {
        _positionUpdateTimer = new System.Timers.Timer(500);
        _positionUpdateTimer.Elapsed += async (_, _) => await UpdatePositions();
        _positionUpdateTimer.Start();
    }

    private void StopPositionPolling()
    {
        _positionUpdateTimer?.Stop();
        _positionUpdateTimer?.Dispose();
        _positionUpdateTimer = null;
    }

    private async Task UpdatePositions()
    {
        if (!IsConnected) return;
        PosX = await _controllerService.ReadRegisterAsync(100);
        PosY = await _controllerService.ReadRegisterAsync(101);
    }

    [RelayCommand]
    private void SetModeManual() { CurrentMode = MachineMode.Manual; StatusMessage = "Mode: Manual"; }

    [RelayCommand]
    private void SetModeAuto() { CurrentMode = MachineMode.Auto; StatusMessage = "Mode: Auto"; }

    [RelayCommand]
    private void HomeAxes()
    {
        CurrentMode = MachineMode.Homing;
        StatusMessage = "Homing all axes...";
        PosX = 0; PosY = 0;
        StatusMessage = "Homing complete.";
        CurrentMode = MachineMode.Manual;
    }

    [RelayCommand]
    private void StartProgram()
    {
        if (SelectedProgram is null) { StatusMessage = "No program selected."; return; }
        IsRunning = true;
        TotalStrokes = SelectedProgram.TotalStrokes;
        CompletedStrokes = 0;
        ProgramName = SelectedProgram.ProgramName;
        StatusMessage = $"Running {ProgramName}...";
    }

    [RelayCommand]
    private void StopProgram()
    {
        IsRunning = false;
        StatusMessage = "Program stopped.";
    }

    [RelayCommand]
    private void EmergencyStop()
    {
        IsRunning = false;
        IsEmergencyStop = true;
        StatusMessage = "⚠ EMERGENCY STOP ACTIVATED";
    }

    [RelayCommand]
    private void ResetEStop()
    {
        IsEmergencyStop = false;
        StatusMessage = "E-Stop reset. Ready.";
    }

    [RelayCommand]
    private void MoveToTarget()
    {
        if (!IsConnected) { StatusMessage = "Not connected."; return; }
        PosX = TargetX; PosY = TargetY;
        StatusMessage = $"Moved to X:{TargetX:F2}  Y:{TargetY:F2}";
    }

    [RelayCommand]
    private void SinglePunch()
    {
        if (!IsConnected) { StatusMessage = "Not connected."; return; }
        StatusMessage = $"Single punch at X:{PosX:F2}  Y:{PosY:F2} with Tool {SelectedToolId}";
        CompletedStrokes++;
    }

    [RelayCommand]
    private void Clear()
    {
        TargetX = 0;
        TargetY = 0;
        CompletedStrokes = 0;
        StatusMessage = "Cleared.";
    }

    [RelayCommand]
    private void SaveHeaderInfo()
    {
        var now = DateTime.Now;
        var lengthValue = ParseValue(Length);
        var widthValue = ParseValue(Width);
        var thicknessValue = ParseValue(Thickness);

        var targetProgram = SelectedProgram;
        if (targetProgram is null)
        {
            targetProgram = Programs.FirstOrDefault(p =>
                !string.IsNullOrWhiteSpace(ProgramName) &&
                string.Equals(p.ProgramName, ProgramName, StringComparison.OrdinalIgnoreCase));
        }

        if (targetProgram is null)
        {
            targetProgram = new PunchProgram
            {
                ProgramId = Programs.Count > 0 ? Programs.Max(p => p.ProgramId) + 1 : 1,
                CreatedBy = "Operator",
                CreatedDate = now
            };
            Programs.Add(targetProgram);
        }

        if (targetProgram.CreatedDate == default)
            targetProgram.CreatedDate = now;
        if (string.IsNullOrWhiteSpace(targetProgram.CreatedBy))
            targetProgram.CreatedBy = "Operator";

        targetProgram.ProgramName = ProgramName;
        targetProgram.Material = Material;
        targetProgram.Comment = Comment;
        targetProgram.Description = Comment;
        targetProgram.ReferenceBending = ReferenceBending;
        targetProgram.Length = lengthValue;
        targetProgram.Width = widthValue;
        targetProgram.Thickness = thicknessValue;
        targetProgram.ModifiedDate = now;
        targetProgram.Steps = PunchSteps.Select(CloneStep).ToList();

        _dataStoreService.SavePunchPrograms(Programs.ToList());
        StatusMessage = "Punching data saved to Database view.";
        MessageBox.Show("Saved successfully. Data is available in Database view.", "Save", MessageBoxButton.OK, MessageBoxImage.Information);
        ClearEditorState();
    }

    [RelayCommand]
    private void ClearHeaderInfo()
    {
        ClearEditorState();
        StatusMessage = "Header info cleared.";
    }

    private void ClearEditorState()
    {
        SelectedProgram = null;
        SelectedStep = null;
        ProgramName = string.Empty;
        Material = string.Empty;
        Comment = string.Empty;
        ReferenceBending = string.Empty;
        Length = string.Empty;
        Width = string.Empty;
        Thickness = string.Empty;
        PunchSteps.Clear();
        UpdateDimensionPreview();
    }

    public void OpenProgramFromDatabase(PunchProgram record)
    {
        var existing = Programs.FirstOrDefault(p => p.ProgramId == record.ProgramId);
        if (existing is null)
        {
            existing = CloneProgram(record);
            Programs.Add(existing);
        }
        else
        {
            existing.ProgramName = record.ProgramName;
            existing.Description = record.Description;
            existing.Material = record.Material;
            existing.Comment = record.Comment;
            existing.Length = record.Length;
            existing.Width = record.Width;
            existing.Thickness = record.Thickness;
            existing.ReferenceBending = record.ReferenceBending;
            existing.ModifiedDate = record.ModifiedDate;
            existing.Steps = record.Steps.Select(CloneStep).ToList();
        }

        SelectedProgram = existing;
        StatusMessage = $"Loaded {existing.ProgramName} from Database.";
    }

    public PunchProgram? GetCurrentProgramSnapshot()
    {
        if (SelectedProgram is null &&
            string.IsNullOrWhiteSpace(ProgramName) &&
            PunchSteps.Count == 0)
        {
            return null;
        }

        var now = DateTime.Now;
        var snapshot = SelectedProgram is null
            ? new PunchProgram
            {
                ProgramId = Programs.Count > 0 ? Programs.Max(p => p.ProgramId) + 1 : 1,
                CreatedBy = "Operator",
                CreatedDate = now,
                ModifiedDate = now
            }
            : CloneProgram(SelectedProgram);

        snapshot.ProgramName = ProgramName;
        snapshot.Material = Material;
        snapshot.Comment = Comment;
        snapshot.Description = Comment;
        snapshot.ReferenceBending = ReferenceBending;
        snapshot.Length = ParseValue(Length);
        snapshot.Width = ParseValue(Width);
        snapshot.Thickness = ParseValue(Thickness);
        snapshot.ModifiedDate = now;
        snapshot.Steps = PunchSteps.Select(CloneStep).ToList();

        return snapshot;
    }

    partial void OnLengthChanged(string value) => UpdateDimensionPreview();

    partial void OnWidthChanged(string value) => UpdateDimensionPreview();

    partial void OnThicknessChanged(string value) => UpdateDimensionPreview();

    public void RefreshToolPreview()
    {
        var result = new ObservableCollection<PunchPreviewShape>();
        if (PunchSteps.Count == 0)
        {
            ToolPreviewShapes = result;
            return;
        }

        var toolsById = _dataStoreService
            .LoadToolRecords()
            .GroupBy(t => t.ToolId)
            .ToDictionary(g => g.Key, g => g.Last());

        var maxStepX = PunchSteps.Count > 0 ? PunchSteps.Max(s => s.X) : 0;
        var maxStepY = PunchSteps.Count > 0 ? PunchSteps.Max(s => s.Y) : 0;
        var sheetLength = Math.Max(ParseValue(Length), Math.Max(maxStepX + 1, 1));
        var sheetWidth = Math.Max(ParseValue(Width), Math.Max(maxStepY + 1, 1));
        var scaleX = PreviewBoxWidth / sheetLength;
        var scaleY = PreviewBoxHeight / sheetWidth;
        var overlapCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var step in PunchSteps)
        {
            var hasTool = toolsById.TryGetValue(step.ToolId, out var toolRecord);
            var isSquare = hasTool && toolRecord is not null && IsSquareToolType(toolRecord.ToolType);

            var toolDiameter = toolRecord?.Diameter ?? 0;
            var toolL = toolRecord?.Length ?? 0;
            var toolW = toolRecord?.Width ?? 0;

            var toolLength = hasTool
                ? (isSquare ? Math.Max(toolL, 1) : Math.Max(toolDiameter, 1))
                : 6;
            var toolWidth = hasTool
                ? (isSquare ? Math.Max(toolW, 1) : Math.Max(toolDiameter, 1))
                : 6;

            var shapeWidth = Clamp(toolLength * scaleX, 12, 54);
            var shapeHeight = Clamp(toolWidth * scaleY, 12, 54);

            var xWithinSheet = Clamp(step.X, 0, sheetLength);
            var yWithinSheet = Clamp(step.Y, 0, sheetWidth);

            var centerX = PreviewFrontLeft + (xWithinSheet * scaleX);
            var centerY = PreviewFrontTop + (yWithinSheet * scaleY);

            var overlapKey = $"{Math.Round(centerX, 1).ToString(CultureInfo.InvariantCulture)}:{Math.Round(centerY, 1).ToString(CultureInfo.InvariantCulture)}";
            overlapCounts.TryGetValue(overlapKey, out var overlapIndex);
            overlapCounts[overlapKey] = overlapIndex + 1;

            // Nudge duplicate coordinates so tools remain visible when multiple steps share the same XY.
            var offsetRadius = Math.Min(3 + (overlapIndex * 2.5), 16);
            var offsetAngle = overlapIndex * 45.0;
            var radians = offsetAngle * (Math.PI / 180.0);
            centerX += Math.Cos(radians) * offsetRadius;
            centerY += Math.Sin(radians) * offsetRadius;

            var left = Clamp(centerX - (shapeWidth / 2), PreviewFrontLeft, PreviewFrontRight - shapeWidth);
            var top = Clamp(centerY - (shapeHeight / 2), PreviewFrontTop, PreviewFrontBottom - shapeHeight);

            var fillColor = BuildFillColor(step.Operation);
            var strokeColor = BuildStrokeColor(step.Operation);
            if (ReferenceEquals(step, SelectedStep))
                strokeColor = "#C62828";

            result.Add(new PunchPreviewShape
            {
                CanvasLeft = left,
                CanvasTop = top,
                ShapeWidth = shapeWidth,
                ShapeHeight = shapeHeight,
                IsRound = !isSquare,
                ToolLabel = $"T{step.ToolId}",
                FillColor = fillColor,
                StrokeColor = strokeColor,
                IsHighlighted = ReferenceEquals(step, SelectedStep)
            });
        }

        ToolPreviewShapes = result;
    }

    private void UpdateDimensionPreview()
    {
        var lengthValue = Math.Max(0, ParseValue(Length));
        var widthValue = Math.Max(0, ParseValue(Width));
        var thicknessValue = Math.Max(0, ParseValue(Thickness));

        const double maxDrawWidth = 180;
        const double maxDrawHeight = 130;
        const double minDrawSize = 24;

        var normalizedLength = Math.Max(lengthValue, 1);
        var normalizedWidth = Math.Max(widthValue, 1);
        var scale = Math.Min(maxDrawWidth / normalizedLength, maxDrawHeight / normalizedWidth);

        PreviewBoxWidth = Clamp(lengthValue * scale, minDrawSize, maxDrawWidth);
        PreviewBoxHeight = Clamp(widthValue * scale, minDrawSize, maxDrawHeight);

        var depthOffset = Clamp(thicknessValue * scale * 0.6, 4, 28);
        PreviewDepthOffsetX = depthOffset;
        PreviewDepthOffsetY = -depthOffset;

        const double canvasWidth = 520;
        const double canvasHeight = 300;
        const double topMargin = 24;

        PreviewFrontLeft = (canvasWidth - PreviewBoxWidth) / 2;
        PreviewFrontTop = ((canvasHeight - 48) - PreviewBoxHeight) / 2 + topMargin;
        PreviewBackLeft = PreviewFrontLeft + PreviewDepthOffsetX;
        PreviewBackTop = PreviewFrontTop + PreviewDepthOffsetY;

        PreviewFrontRight = PreviewFrontLeft + PreviewBoxWidth;
        PreviewFrontBottom = PreviewFrontTop + PreviewBoxHeight;
        PreviewBackRight = PreviewBackLeft + PreviewBoxWidth;
        PreviewBackBottom = PreviewBackTop + PreviewBoxHeight;

        LengthArrowY = PreviewFrontBottom + 12;
        LengthLabelX = PreviewFrontLeft + (PreviewBoxWidth / 2) - 26;
        LengthLabelY = LengthArrowY + 2;
        LengthLabelText = $"L = {lengthValue:F3}";

        WidthArrowX = PreviewFrontLeft - 14;
        WidthLabelX = WidthArrowX - 78;
        WidthLabelY = PreviewFrontTop + (PreviewBoxHeight / 2) - 8;
        WidthLabelText = $"W = {widthValue:F3}";

        ThicknessLabelX = PreviewBackRight + 6;
        ThicknessLabelY = PreviewBackTop - 10;
        ThicknessLabelText = $"T = {thicknessValue:F3}";

        PreviewDimensionText = $"L: {lengthValue:F3}  W: {widthValue:F3}  T: {thicknessValue:F3}";
        RefreshToolPreview();
    }

    [RelayCommand]
    private void ZoomInPreview()
    {
        PreviewZoom = Clamp(PreviewZoom + 0.1, 0.6, 3.0);
    }

    [RelayCommand]
    private void ZoomOutPreview()
    {
        PreviewZoom = Clamp(PreviewZoom - 0.1, 0.6, 3.0);
    }

    [RelayCommand]
    private void ResetPreviewZoom()
    {
        PreviewZoom = 1.0;
    }

    private static double ParseValue(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0;

        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;

        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            return value;

        return 0;
    }

    private static string FormatValue(double value)
        => value.ToString("F3", CultureInfo.InvariantCulture);

    private static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private static bool IsSquareToolType(string? rawType)
    {
        var value = (rawType ?? string.Empty).Trim().ToLowerInvariant();
        return value.StartsWith("sq") || value.Contains("squ") || value == "square";
    }

    private static PunchStep CloneStep(PunchStep step)
    {
        return new PunchStep
        {
            StepNumber = step.StepNumber,
            X = step.X,
            Y = step.Y,
            M = step.M,
            F = step.F,
            ToolId = step.ToolId,
            Operation = step.Operation,
            IsCompleted = step.IsCompleted
        };
    }

    private static PunchProgram CloneProgram(PunchProgram source)
    {
        return new PunchProgram
        {
            ProgramId = source.ProgramId,
            ProgramName = source.ProgramName,
            Description = source.Description,
            Material = source.Material,
            Comment = source.Comment,
            Length = source.Length,
            Width = source.Width,
            Thickness = source.Thickness,
            ReferenceBending = source.ReferenceBending,
            CreatedDate = source.CreatedDate,
            ModifiedDate = source.ModifiedDate,
            CreatedBy = source.CreatedBy,
            Steps = source.Steps.Select(CloneStep).ToList()
        };
    }

    private static string BuildFillColor(string? operation)
    {
        var (r, g, b) = BuildOperationRgb(operation);
        return $"#88{r:X2}{g:X2}{b:X2}";
    }

    private static string BuildStrokeColor(string? operation)
    {
        var (r, g, b) = BuildOperationRgb(operation);
        r = (int)(r * 0.65);
        g = (int)(g * 0.65);
        b = (int)(b * 0.65);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static (int R, int G, int B) BuildOperationRgb(string? operation)
    {
        var text = string.IsNullOrWhiteSpace(operation) ? "PUNCH" : operation?.Trim().ToUpperInvariant() ?? "PUNCH";
        int hash = 17;
        foreach (var ch in text)
            hash = (hash * 31) + ch;

        var hue = Math.Abs(hash % 360);
        return HsvToRgb(hue, 0.65, 0.9);
    }

    private static (int R, int G, int B) HsvToRgb(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs(((h / 60.0) % 2) - 1));
        var m = v - c;

        double r1, g1, b1;
        if (h < 60) { r1 = c; g1 = x; b1 = 0; }
        else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
        else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
        else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
        else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
        else { r1 = c; g1 = 0; b1 = x; }

        var r = (int)Math.Round((r1 + m) * 255);
        var g = (int)Math.Round((g1 + m) * 255);
        var b = (int)Math.Round((b1 + m) * 255);
        return (r, g, b);
    }
}

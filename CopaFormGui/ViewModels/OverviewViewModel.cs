using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Models;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;

public partial class OverviewViewModel : ObservableObject
{
    private readonly IControllerService _controllerService;
    private readonly IDataStoreService _dataStoreService;
    private System.Timers.Timer? _pollTimer;

    [ObservableProperty] private bool _isConnected;

    // Axis positions
    [ObservableProperty] private double _posX;
    [ObservableProperty] private double _posY;

    // 2-D preview (same virtual canvas setup used in Program Editor)
    [ObservableProperty] private ObservableCollection<PunchPreviewPoint> _previewPoints = new();
    [ObservableProperty] private ObservableCollection<PunchPreviewShape> _toolPreviewShapes = new();
    [ObservableProperty] private double _previewSheetLeft = 110;
    [ObservableProperty] private double _previewSheetTop = 70;
    [ObservableProperty] private double _previewSheetWidth = 300;
    [ObservableProperty] private double _previewSheetHeight = 150;
    [ObservableProperty] private double _previewZoom = 1.5;

    // Machine state
    [ObservableProperty] private string _machineMode = "Manual";
    [ObservableProperty] private bool _isMachineRunning;
    [ObservableProperty] private bool _isEStop;
    [ObservableProperty] private bool _isAlarmActive;
    [ObservableProperty] private string _activeAlarmText = "No active alarms";

    // Current job
    [ObservableProperty] private string _currentJob = "—";
    [ObservableProperty] private int _completedStrokes;
    [ObservableProperty] private int _totalStrokes = 1;
    [ObservableProperty] private double _progressPct;

    // Spindle / punch
    [ObservableProperty] private double _punchForce;
    [ObservableProperty] private double _airPressure = 6.2;

    // Controller info
    [ObservableProperty] private string _controllerIp = "172.20.0.200";
    [ObservableProperty] private string _firmwareVersion = "v3.1.4";
    [ObservableProperty] private string _lastConnected = "—";
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private string _recentComment = string.Empty;
    [ObservableProperty] private string _recentMaterial = string.Empty;
    [ObservableProperty] private string _recentLengthWidth = string.Empty;
    [ObservableProperty] private string _recentThickness = string.Empty;
    [ObservableProperty] private string _recentLengthText = string.Empty;
    [ObservableProperty] private string _recentWidthText = string.Empty;
    [ObservableProperty] private string _recentThicknessText = string.Empty;

    // POC test I/O
    [ObservableProperty] private string _testVariableName = "P100";
    [ObservableProperty] private string _testWriteValue = "0";
    [ObservableProperty] private string _testReadValue = "—";
    [ObservableProperty] private bool _isTestWriteFailed;

    private CancellationTokenSource? _writeDebounceCts;

    // Quick-status tiles
    public string ModeColor => IsMachineRunning ? "#28A745" : "#1565C0";
    public string EStopColor => IsEStop ? "#DC3545" : "#BDBDBD";
    public string AlarmColor => IsAlarmActive ? "#FFC107" : "#BDBDBD";

    public OverviewViewModel(IControllerService controllerService, IDataStoreService dataStoreService)
    {
        _controllerService = controllerService;
        _dataStoreService = dataStoreService;
        _controllerService.ConnectionStateChanged += OnConnectionStateChanged;
        IsConnected = controllerService.IsConnected;
        ControllerIp = string.IsNullOrWhiteSpace(_controllerService.CurrentIpAddress)
            ? ControllerIp
            : _controllerService.CurrentIpAddress!;
        ReloadLatestFromDatabase();
        if (IsConnected) StartPolling();
    }

    public void ReloadLatestFromDatabase()
    {
        var programs = _dataStoreService.LoadPunchPrograms();
        if (programs.Count == 0)
        {
            RecentComment = string.Empty;
            RecentMaterial = string.Empty;
            RecentLengthWidth = "0.000 x 0.000";
            RecentThickness = "0.000";
            RecentLengthText = "0.000 mm";
            RecentWidthText = "0.000 mm";
            RecentThicknessText = "0.000 mm";
            InitializeDefaultPreview();
            return;
        }

        var latest = SelectBestDatabaseProgram(programs);

        LoadFromProgram(latest);
    }

    public void LoadFromPunchingOrDatabase(PunchProgram? candidate)
    {
        if (HasUsableHeaderData(candidate) || HasUsableStepData(candidate))
        {
            LoadFromProgram(candidate);
            return;
        }

        ReloadLatestFromDatabase();
    }

    private static PunchProgram SelectBestDatabaseProgram(List<PunchProgram> programs)
    {
        var withSteps = programs
            .Where(p => p.Steps is not null && p.Steps.Count > 0)
            .OrderByDescending(p => p.ModifiedDate)
            .ThenByDescending(p => p.CreatedDate)
            .ThenByDescending(p => p.ProgramId)
            .FirstOrDefault();

        if (withSteps is not null)
            return withSteps;

        return programs
            .OrderByDescending(p => p.ModifiedDate)
            .ThenByDescending(p => p.CreatedDate)
            .ThenByDescending(p => p.ProgramId)
            .First();
    }

    public void LoadFromProgram(PunchProgram? program)
    {
        if (program is null)
        {
            ReloadLatestFromDatabase();
            return;
        }

        var safeLength = Math.Max(0, program.Length);
        var safeWidth = Math.Max(0, program.Width);
        var safeThickness = Math.Max(0, program.Thickness);

        RecentComment = program.Comment;
        RecentMaterial = program.Material;
        RecentLengthWidth = $"{safeLength:F3} x {safeWidth:F3}";
        RecentThickness = $"{safeThickness:F3}";
        RecentLengthText = $"{safeLength:F3} mm";
        RecentWidthText = $"{safeWidth:F3} mm";
        RecentThicknessText = $"{safeThickness:F3} mm";
        RenderProgramPreview(program);
    }

    private static bool HasUsableHeaderData(PunchProgram? program)
    {
        if (program is null) return false;
        return program.Length > 0 || program.Width > 0 || program.Thickness > 0;
    }

    private static bool HasUsableStepData(PunchProgram? program)
    {
        if (program?.Steps is null || program.Steps.Count == 0) return false;
        return program.Steps.Any(s => s.X != 0 || s.Y != 0 || s.ToolId != 0);
    }

    private void InitializeDefaultPreview()
    {
        // Show a fallback 4-hole pattern when no saved punch program exists yet.
        var points = new ObservableCollection<PunchPreviewPoint>
        {
            new() { CanvasX = 140, CanvasY = 95, IsSelected = true },
            new() { CanvasX = 380, CanvasY = 95, IsSelected = false },
            new() { CanvasX = 140, CanvasY = 190, IsSelected = false },
            new() { CanvasX = 380, CanvasY = 190, IsSelected = false }
        };

        PreviewPoints = points;
        ToolPreviewShapes = new ObservableCollection<PunchPreviewShape>(
            points.Select(p => new PunchPreviewShape
            {
                CanvasLeft = p.CanvasX - 6,
                CanvasTop = p.CanvasY - 6,
                ShapeWidth = 12,
                ShapeHeight = 12,
                IsRound = true,
                FillColor = "#44FF7A00",
                StrokeColor = "#C75A00"
            }));
    }

    private void RenderProgramPreview(PunchProgram program)
    {
        var maxX = program.Steps.Count > 0 ? program.Steps.Max(s => s.X) : 100;
        var maxY = program.Steps.Count > 0 ? program.Steps.Max(s => s.Y) : 60;

        var rawLength = program.Length > 0 ? program.Length : (maxX + 10);
        var rawWidth = program.Width > 0 ? program.Width : (maxY + 10);

        var sheetLength = Math.Max(rawLength, 1);
        var sheetWidth = Math.Max(rawWidth, 1);

        const double canvasWidth = 520;
        const double canvasHeight = 300;
        const double maxDrawWidth = 320;
        const double maxDrawHeight = 170;
        const double minDrawWidth = 120;
        const double minDrawHeight = 70;

        var scale = Math.Min(maxDrawWidth / sheetLength, maxDrawHeight / sheetWidth);
        var drawWidth = Clamp(sheetLength * scale, minDrawWidth, maxDrawWidth);
        var drawHeight = Clamp(sheetWidth * scale, minDrawHeight, maxDrawHeight);

        PreviewSheetWidth = drawWidth;
        PreviewSheetHeight = drawHeight;
        PreviewSheetLeft = (canvasWidth - drawWidth) / 2;
        PreviewSheetTop = (canvasHeight - drawHeight) / 2;

        var scaleX = drawWidth / sheetLength;
        var scaleY = drawHeight / sheetWidth;
        var overlapCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        var points = program.Steps
            .Select((step, index) => new PunchPreviewPoint
            {
                CanvasX = PreviewSheetLeft + (Clamp(step.X, 0, sheetLength) * scaleX),
                CanvasY = PreviewSheetTop + (Clamp(step.Y, 0, sheetWidth) * scaleY),
                IsSelected = index == 0
            })
            .ToList();

        PreviewPoints = new ObservableCollection<PunchPreviewPoint>(points);

        var toolsById = _dataStoreService
            .LoadToolRecords()
            .GroupBy(t => t.ToolId)
            .ToDictionary(g => g.Key, g => g.Last());

        var shapes = new List<PunchPreviewShape>();
        foreach (var step in program.Steps)
        {
            var hasTool = toolsById.TryGetValue(step.ToolId, out var toolRecord);
            var isSquare = hasTool && toolRecord is not null && IsSquareToolType(toolRecord.ToolType);

            var diameter = toolRecord?.Diameter ?? 0;
            var toolL = toolRecord?.Length ?? 0;
            var toolW = toolRecord?.Width ?? 0;

            var toolLength = hasTool
                ? (isSquare ? Math.Max(toolL, 1) : Math.Max(diameter, 1))
                : 6;
            var toolWidth = hasTool
                ? (isSquare ? Math.Max(toolW, 1) : Math.Max(diameter, 1))
                : 6;

            var shapeWidth = Clamp(toolLength * scaleX, 12, 54);
            var shapeHeight = Clamp(toolWidth * scaleY, 12, 54);

            var x = Clamp(step.X, 0, sheetLength);
            var y = Clamp(step.Y, 0, sheetWidth);
            var centerX = PreviewSheetLeft + (x * scaleX);
            var centerY = PreviewSheetTop + (y * scaleY);

            var overlapKey = $"{Math.Round(centerX, 1).ToString(CultureInfo.InvariantCulture)}:{Math.Round(centerY, 1).ToString(CultureInfo.InvariantCulture)}";
            overlapCounts.TryGetValue(overlapKey, out var overlapIndex);
            overlapCounts[overlapKey] = overlapIndex + 1;

            var offsetRadius = Math.Min(3 + (overlapIndex * 2.5), 16);
            var offsetAngle = overlapIndex * 45.0;
            var radians = offsetAngle * (Math.PI / 180.0);
            centerX += Math.Cos(radians) * offsetRadius;
            centerY += Math.Sin(radians) * offsetRadius;

            var left = Clamp(centerX - (shapeWidth / 2), PreviewSheetLeft, PreviewSheetLeft + PreviewSheetWidth - shapeWidth);
            var top = Clamp(centerY - (shapeHeight / 2), PreviewSheetTop, PreviewSheetTop + PreviewSheetHeight - shapeHeight);

            shapes.Add(new PunchPreviewShape
            {
                CanvasLeft = left,
                CanvasTop = top,
                ShapeWidth = shapeWidth,
                ShapeHeight = shapeHeight,
                IsRound = !isSquare,
                FillColor = "#44FF7A00",
                StrokeColor = "#C75A00"
            });
        }

        ToolPreviewShapes = new ObservableCollection<PunchPreviewShape>(shapes);
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
        PreviewZoom = 1.5;
    }

    private static bool IsSquareToolType(string? rawType)
    {
        var value = (rawType ?? string.Empty).Trim().ToLowerInvariant();
        return value.StartsWith("sq") || value.Contains("squ") || value == "square";
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        IsConnected = state == ConnectionState.Connected;
        if (!string.IsNullOrWhiteSpace(_controllerService.CurrentIpAddress))
            ControllerIp = _controllerService.CurrentIpAddress!;
        LastConnected = IsConnected ? DateTime.Now.ToString("HH:mm:ss  dd/MM/yyyy") : LastConnected;
        if (IsConnected) StartPolling(); else StopPolling();
        OnPropertyChanged(nameof(ModeColor));
    }

    private void StartPolling()
    {
        _pollTimer = new System.Timers.Timer(800);
        _pollTimer.Elapsed += async (_, _) => await PollAsync();
        _pollTimer.Start();
    }

    private void StopPolling()
    {
        _pollTimer?.Stop();
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    private async Task PollAsync()
    {
        if (!IsConnected) return;
        PosX = await _controllerService.ReadRegisterAsync(100);
        PosY = await _controllerService.ReadRegisterAsync(101);
        PunchForce = await _controllerService.ReadRegisterAsync(110);

        var testValue = await _controllerService.ReadResponseAsync(TestVariableName);
        if (!string.IsNullOrWhiteSpace(testValue))
        {
            TestReadValue = testValue;
        }

        AirPressure = 5.8 + (PunchForce % 1.0);
        ProgressPct = TotalStrokes > 0 ? Math.Min(100.0, CompletedStrokes * 100.0 / TotalStrokes) : 0;
    }

    partial void OnTestWriteValueChanged(string value)
    {
        if (!IsConnected) return;
        DebounceWriteTestValue(value);
    }

    partial void OnTestVariableNameChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            TestVariableName = "P100";
    }

    private void DebounceWriteTestValue(string rawValue)
    {
        _writeDebounceCts?.Cancel();
        _writeDebounceCts?.Dispose();
        _writeDebounceCts = new CancellationTokenSource();
        var token = _writeDebounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(350, token);
                if (token.IsCancellationRequested) return;
                await WriteTestValueInternalAsync(rawValue);
            }
            catch (OperationCanceledException)
            {
                // User is still typing.
            }
        }, token);
    }

    private async Task WriteTestValueInternalAsync(string rawValue)
    {
        if (!double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue))
        {
            IsTestWriteFailed = true;
            return;
        }

        var success = await _controllerService.WriteVariableAsync(TestVariableName, parsedValue);
        IsTestWriteFailed = !success;
        if (success)
        {
            var latestValue = await _controllerService.ReadResponseAsync(TestVariableName);
            if (!string.IsNullOrWhiteSpace(latestValue))
                TestReadValue = latestValue;
        }
    }

    [RelayCommand]
    private async Task ReadTestVariableAsync()
    {
        var value = await _controllerService.ReadResponseAsync(TestVariableName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            TestReadValue = value;
            IsTestWriteFailed = false;
        }
        else
        {
            IsTestWriteFailed = true;
        }
    }

    [RelayCommand]
    private void EmergencyStop()
    {
        IsEStop = true;
        IsMachineRunning = false;
        StatusMessage = "⚠ EMERGENCY STOP ACTIVATED";
        OnPropertyChanged(nameof(EStopColor));
    }

    [RelayCommand]
    private void ResetAlarms()
    {
        IsAlarmActive = false;
        IsEStop = false;
        ActiveAlarmText = "No active alarms";
        StatusMessage = "Alarms cleared.";
        OnPropertyChanged(nameof(AlarmColor));
        OnPropertyChanged(nameof(EStopColor));
    }
}

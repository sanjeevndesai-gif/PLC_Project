using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Models;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;

public partial class ProgramEditorViewModel : ObservableObject
{
    private readonly IControllerService _controllerService;
    private readonly IDataStoreService _dataStoreService;

    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _statusMessage = "Select a program to view or edit its steps";

    [ObservableProperty]
    private ObservableCollection<PunchProgram> _programs = new();

    [ObservableProperty]
    private PunchProgram? _selectedProgram;

    [ObservableProperty]
    private ObservableCollection<PunchStep> _steps = new();

    [ObservableProperty]
    private PunchStep? _selectedStep;

    // --- 2-D punch preview (virtual canvas 800 × 150) ---
    [ObservableProperty] private ObservableCollection<PunchPreviewPoint> _previewPoints = new();
    [ObservableProperty] private double _previewSheetLeft   = 80;
    [ObservableProperty] private double _previewSheetTop    = 10;
    [ObservableProperty] private double _previewSheetWidth  = 640;
    [ObservableProperty] private double _previewSheetHeight = 110;

    // Step edit fields
    [ObservableProperty] private double _editX;
    [ObservableProperty] private double _editY;
    [ObservableProperty] private int _editToolId = 1;
    [ObservableProperty] private string _editOperation = "Punch";

    // Program header edit fields
    [ObservableProperty] private string _editProgramName = string.Empty;
    [ObservableProperty] private string _editDescription = string.Empty;

    public string[] Operations { get; } = { "Punch", "Move", "Dwell", "Clamp", "Unclamp" };

    public ProgramEditorViewModel(IControllerService controllerService, IDataStoreService dataStoreService)
    {
        _controllerService = controllerService;
        _dataStoreService = dataStoreService;
        _controllerService.ConnectionStateChanged += (_, s) => IsConnected = s == ConnectionState.Connected;
        IsConnected = controllerService.IsConnected;
        LoadPrograms();
        SelectedProgram = Programs.FirstOrDefault();
    }

    private void LoadPrograms()
    {
        var storedPrograms = _dataStoreService.LoadPunchPrograms();
        if (storedPrograms.Count > 0)
        {
            Programs = new ObservableCollection<PunchProgram>(storedPrograms);
            return;
        }

        Programs = new ObservableCollection<PunchProgram>
        {
            new()
            {
                ProgramId = 1, ProgramName = "PROG_001", Description = "Flange Pattern A", CreatedBy = "Admin",
                Steps = Enumerable.Range(1, 8).Select(i => new PunchStep
                {
                    StepNumber = i, X = i * 50.0, Y = 100.0, ToolId = 1, Operation = "Punch"
                }).ToList()
            },
            new()
            {
                ProgramId = 2, ProgramName = "PROG_002", Description = "Bracket Pattern B", CreatedBy = "Admin",
                Steps = Enumerable.Range(1, 12).Select(i => new PunchStep
                {
                    StepNumber = i, X = i * 40.0, Y = 200.0, ToolId = 2, Operation = "Punch"
                }).ToList()
            },
            new()
            {
                ProgramId = 3, ProgramName = "PROG_003", Description = "Panel Pattern C", CreatedBy = "Operator",
                Steps = Enumerable.Range(1, 16).Select(i => new PunchStep
                {
                    StepNumber = i, X = (i % 4) * 60.0, Y = (i / 4) * 60.0, ToolId = 1, Operation = "Punch"
                }).ToList()
            }
        };

        _dataStoreService.SavePunchPrograms(Programs.ToList());
    }

    partial void OnSelectedProgramChanged(PunchProgram? value)
    {
        if (value is null) { Steps.Clear(); RefreshPreview(); return; }
        Steps = new ObservableCollection<PunchStep>(value.Steps);
        EditProgramName = value.ProgramName;
        EditDescription = value.Description;
        StatusMessage = $"Program {value.ProgramName} loaded – {value.Steps.Count} steps";
        RefreshPreview();
    }

    partial void OnSelectedStepChanged(PunchStep? value)
    {
        if (value is null) return;
        EditX = value.X;
        EditY = value.Y;
        EditToolId = value.ToolId;
        EditOperation = value.Operation;
    }

    [RelayCommand]
    private void SaveStep()
    {
        if (SelectedStep is null) { StatusMessage = "No step selected."; return; }

        var idx = Steps.IndexOf(SelectedStep);
        if (idx < 0) { StatusMessage = "Selected step not found."; return; }

        var updatedStep = new PunchStep
        {
            StepNumber = SelectedStep.StepNumber,
            X = EditX,
            Y = EditY,
            ToolId = EditToolId,
            Operation = EditOperation,
            IsCompleted = SelectedStep.IsCompleted
        };

        Steps[idx] = updatedStep;
        if (SelectedProgram is not null)
        {
            if (idx < SelectedProgram.Steps.Count)
                SelectedProgram.Steps[idx] = CloneStep(updatedStep);
            else
                SelectedProgram.Steps = Steps.Select(CloneStep).ToList();
        }

        SelectedStep = updatedStep;
        StatusMessage = $"Step {updatedStep.StepNumber} saved.";
        _dataStoreService.SavePunchPrograms(Programs.ToList());
        RefreshPreview();
    }

    [RelayCommand]
    private void AddStep()
    {
        if (SelectedProgram is null) { StatusMessage = "No program selected."; return; }
        var step = new PunchStep
        {
            StepNumber = Steps.Count + 1,
            X = EditX, Y = EditY,
            ToolId = EditToolId, Operation = EditOperation
        };
        Steps.Add(step);
        SelectedProgram.Steps.Add(CloneStep(step));
        SelectedStep = step;
        StatusMessage = $"Step {step.StepNumber} added.";
        _dataStoreService.SavePunchPrograms(Programs.ToList());
        RefreshPreview();
    }

    [RelayCommand]
    private void DeleteStep()
    {
        if (SelectedStep is null) { StatusMessage = "No step selected."; return; }

        var idx = Steps.IndexOf(SelectedStep);
        if (idx < 0) { StatusMessage = "Selected step not found."; return; }

        Steps.RemoveAt(idx);
        if (SelectedProgram is not null && idx < SelectedProgram.Steps.Count)
            SelectedProgram.Steps.RemoveAt(idx);

        for (int i = 0; i < Steps.Count; i++) Steps[i].StepNumber = i + 1;

        if (SelectedProgram is not null)
            SelectedProgram.Steps = Steps.Select(CloneStep).ToList();

        SelectedStep = null;
        StatusMessage = "Step deleted and list renumbered.";
        _dataStoreService.SavePunchPrograms(Programs.ToList());
        RefreshPreview();
    }

    [RelayCommand]
    private void NewProgram()
    {
        var prog = new PunchProgram
        {
            ProgramId = Programs.Count + 1,
            ProgramName = $"PROG_{Programs.Count + 1:000}",
            Description = "New Program",
            CreatedBy = "Operator"
        };
        Programs.Add(prog);
        SelectedProgram = prog;
        StatusMessage = $"New program {prog.ProgramName} created.";
        _dataStoreService.SavePunchPrograms(Programs.ToList());
    }

    [RelayCommand]
    private void SaveProgram()
    {
        if (SelectedProgram is null) { StatusMessage = "No program selected."; return; }

        var idx = Programs.IndexOf(SelectedProgram);
        if (idx < 0) { StatusMessage = "Selected program not found."; return; }

        var updatedProgram = new PunchProgram
        {
            ProgramId = SelectedProgram.ProgramId,
            ProgramName = EditProgramName,
            Description = EditDescription,
            CreatedBy = SelectedProgram.CreatedBy,
            CreatedDate = SelectedProgram.CreatedDate,
            ModifiedDate = DateTime.Now,
            Steps = Steps.Select(CloneStep).ToList()
        };

        Programs[idx] = updatedProgram;
        SelectedProgram = updatedProgram;
        StatusMessage = $"Program {updatedProgram.ProgramName} saved.";
        _dataStoreService.SavePunchPrograms(Programs.ToList());
    }

    private static PunchStep CloneStep(PunchStep step)
    {
        return new PunchStep
        {
            StepNumber = step.StepNumber,
            X = step.X,
            Y = step.Y,
            ToolId = step.ToolId,
            Operation = step.Operation,
            IsCompleted = step.IsCompleted
        };
    }

    // -------------------------------------------------------------------------
    // 2-D preview – virtual canvas 800 × 150 px
    // -------------------------------------------------------------------------
    public void RefreshPreview()
    {
        // Reserve left margin for axis arrows; small margins on other sides
        const double DrawLeft = 90, DrawTop = 12, DrawRight = 786, DrawBottom = 132;
        double availW = DrawRight - DrawLeft;
        double availH = DrawBottom - DrawTop;

        var pts = Steps.ToList();

        if (pts.Count == 0)
        {
            PreviewSheetLeft   = DrawLeft;
            PreviewSheetTop    = DrawTop;
            PreviewSheetWidth  = availW;
            PreviewSheetHeight = availH;
            PreviewPoints = new ObservableCollection<PunchPreviewPoint>();
            return;
        }

        double xMin = pts.Min(s => s.X), xMax = pts.Max(s => s.X);
        double yMin = pts.Min(s => s.Y), yMax = pts.Max(s => s.Y);

        // Pad the bounding box so holes are never on the sheet edge
        const double Pad = 15.0;
        double pxMin = xMin - Pad, pxMax = xMax + Pad;
        double pyMin = yMin - Pad, pyMax = yMax + Pad;
        double dataW = pxMax - pxMin, dataH = pyMax - pyMin;

        // Uniform scale – fit the sheet inside the available area
        double scale = Math.Min(
            dataW > 0 ? availW / dataW : 1.0,
            dataH > 0 ? availH / dataH : 1.0);

        double sheetW = dataW * scale;
        double sheetH = dataH * scale;

        // Centre the sheet inside the available drawing area
        double sheetLeft = DrawLeft + (availW - sheetW) / 2;
        double sheetTop  = DrawTop  + (availH - sheetH) / 2;

        PreviewSheetLeft   = sheetLeft;
        PreviewSheetTop    = sheetTop;
        PreviewSheetWidth  = sheetW;
        PreviewSheetHeight = sheetH;

        var points = new ObservableCollection<PunchPreviewPoint>();
        foreach (var s in pts)
        {
            points.Add(new PunchPreviewPoint
            {
                CanvasX    = sheetLeft + (s.X - pxMin) * scale,
                CanvasY    = sheetTop  + (pyMax - s.Y) * scale,  // Y flipped
                IsSelected = s == SelectedStep
            });
        }
        PreviewPoints = points;
    }
}

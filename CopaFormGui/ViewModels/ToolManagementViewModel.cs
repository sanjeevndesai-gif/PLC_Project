using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Models;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;

public partial class ToolManagementViewModel : ObservableObject
{
    private readonly IControllerService _controllerService;

    [ObservableProperty] private bool _isConnected;

    [ObservableProperty]
    private ObservableCollection<ToolRecord> _tools = new();

    [ObservableProperty]
    private ToolRecord? _selectedTool;

    // Edit fields
    [ObservableProperty] private int _editToolId;
    [ObservableProperty] private string _editToolName = string.Empty;
    [ObservableProperty] private string _editToolType = "Round";
    [ObservableProperty] private double _editDiameter;
    [ObservableProperty] private double _editLength;
    [ObservableProperty] private double _editStrokeLength;
    [ObservableProperty] private int _editMaxStrokes;
    [ObservableProperty] private int _editCurrentStrokes;
    [ObservableProperty] private string _editNotes = string.Empty;

    [ObservableProperty] private string _statusMessage = "Select a tool to edit.";

    public IEnumerable<string> ToolTypes { get; } = new[] { "Round", "Square", "Oblong", "Hex", "Triangle", "Custom" };

    public ToolManagementViewModel(IControllerService controllerService)
    {
        _controllerService = controllerService;
        _controllerService.ConnectionStateChanged += (_, s) => IsConnected = s == ConnectionState.Connected;
        IsConnected = controllerService.IsConnected;
        LoadTools();
    }

    private void LoadTools()
    {
        Tools = new ObservableCollection<ToolRecord>
        {
            new() { ToolId = 1, ToolName = "Round Punch 10mm",  ToolType = "Round",    Diameter = 10.0, Length = 50.0, StrokeLength = 30.0, MaxStrokes = 5000, CurrentStrokes = 1230, Status = "OK" },
            new() { ToolId = 2, ToolName = "Square Punch 8mm",  ToolType = "Square",   Diameter =  8.0, Length = 50.0, StrokeLength = 28.0, MaxStrokes = 4000, CurrentStrokes =  870, Status = "OK" },
            new() { ToolId = 3, ToolName = "Oblong 15x8mm",     ToolType = "Oblong",   Diameter = 15.0, Length = 55.0, StrokeLength = 32.0, MaxStrokes = 3000, CurrentStrokes = 2990, Status = "Warn" },
            new() { ToolId = 4, ToolName = "Round Punch 6mm",   ToolType = "Round",    Diameter =  6.0, Length = 45.0, StrokeLength = 25.0, MaxStrokes = 6000, CurrentStrokes =  350, Status = "OK" },
            new() { ToolId = 5, ToolName = "Hex Punch 12mm",    ToolType = "Hex",      Diameter = 12.0, Length = 52.0, StrokeLength = 30.0, MaxStrokes = 4500, CurrentStrokes = 4490, Status = "Warn" },
        };
    }

    partial void OnSelectedToolChanged(ToolRecord? value)
    {
        if (value is null) return;
        EditToolId = value.ToolId;
        EditToolName = value.ToolName;
        EditToolType = value.ToolType;
        EditDiameter = value.Diameter;
        EditLength = value.Length;
        EditStrokeLength = value.StrokeLength;
        EditMaxStrokes = value.MaxStrokes;
        EditCurrentStrokes = value.CurrentStrokes;
        EditNotes = value.Notes;
    }

    [RelayCommand]
    private void AddTool()
    {
        var tool = new ToolRecord
        {
            ToolId = Tools.Count > 0 ? Tools.Max(t => t.ToolId) + 1 : 1,
            ToolName = "New Tool",
            ToolType = "Round",
            Diameter = 10.0,
            Length = 50.0,
            StrokeLength = 30.0,
            MaxStrokes = 5000,
            Status = "OK"
        };
        Tools.Add(tool);
        SelectedTool = tool;
        StatusMessage = "New tool added. Fill in the details and save.";
    }

    [RelayCommand]
    private void SaveTool()
    {
        if (SelectedTool is null) return;
        SelectedTool.ToolName = EditToolName;
        SelectedTool.ToolType = EditToolType;
        SelectedTool.Diameter = EditDiameter;
        SelectedTool.Length = EditLength;
        SelectedTool.StrokeLength = EditStrokeLength;
        SelectedTool.MaxStrokes = EditMaxStrokes;
        SelectedTool.CurrentStrokes = EditCurrentStrokes;
        SelectedTool.Notes = EditNotes;
        StatusMessage = $"Tool '{EditToolName}' saved.";
        // Refresh list view
        var idx = Tools.IndexOf(SelectedTool);
        Tools[idx] = SelectedTool;
    }

    [RelayCommand]
    private void DeleteTool()
    {
        if (SelectedTool is null) return;
        Tools.Remove(SelectedTool);
        SelectedTool = null;
        StatusMessage = "Tool deleted.";
    }

    [RelayCommand]
    private void ResetStrokes()
    {
        if (SelectedTool is null) return;
        SelectedTool.CurrentStrokes = 0;
        EditCurrentStrokes = 0;
        StatusMessage = $"Stroke count reset for '{SelectedTool.ToolName}'.";
    }
}

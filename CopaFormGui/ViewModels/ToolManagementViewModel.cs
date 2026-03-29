using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Models;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;

public partial class ToolManagementViewModel : ObservableObject
{
    private readonly IControllerService _controllerService;
    private readonly IDataStoreService _dataStoreService;

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
    [ObservableProperty] private double _editWidth;
    [ObservableProperty] private string _editNotes = string.Empty;
    [ObservableProperty] private bool _editIsUsed = false;

    [ObservableProperty] private string _statusMessage = "Select a tool to edit.";
    [ObservableProperty] private bool _isStatusSuccess;

    [ObservableProperty] private bool _isRoundTool = true;
    [ObservableProperty] private bool _isSquareTool = false;

    public IEnumerable<string> ToolTypes { get; } = new[] { "Round", "Square" };

    public ToolManagementViewModel(IControllerService controllerService, IDataStoreService dataStoreService)
    {
        _controllerService = controllerService;
        _dataStoreService = dataStoreService;
        _controllerService.ConnectionStateChanged += (_, s) => IsConnected = s == ConnectionState.Connected;
        IsConnected = controllerService.IsConnected;
        LoadTools();
    }

    private void LoadTools()
    {
        var storedTools = _dataStoreService.LoadToolRecords();
        if (storedTools.Count > 0)
        {
            Tools = new ObservableCollection<ToolRecord>(storedTools);
            return;
        }

        Tools = new ObservableCollection<ToolRecord>
        {
            new() { ToolId = 1, ToolName = "Round Punch 10mm",  ToolType = "Round",  Diameter = 10.0, Length = 0.0, Width = 0.0 },
            new() { ToolId = 2, ToolName = "Square Punch 8x8", ToolType = "Square", Diameter = 0.0, Length = 8.0, Width = 8.0 },
            new() { ToolId = 3, ToolName = "Round Punch 6mm",   ToolType = "Round",  Diameter = 6.0, Length = 0.0, Width = 0.0 },
            new() { ToolId = 4, ToolName = "Square Punch 12x10", ToolType = "Square", Diameter = 0.0, Length = 12.0, Width = 10.0 },
        };

        _dataStoreService.SaveToolRecords(Tools.ToList());
    }

    partial void OnSelectedToolChanged(ToolRecord? value)
    {
        if (value is null) return;
        EditToolId = value.ToolId;
        EditToolName = value.ToolName;
        EditToolType = NormalizeToolType(value.ToolType);
        EditDiameter = value.Diameter;
        EditLength = value.Length;
        EditWidth = value.Width;
        EditNotes = value.Notes;
        EditIsUsed = value.IsUsed;
        SetToolTypeFlags(EditToolType);
        IsStatusSuccess = false;
        StatusMessage = "Edit the fields and click Save Tool.";
    }

    partial void OnEditToolTypeChanged(string value)
    {
        var normalizedType = NormalizeToolType(value);
        if (!string.Equals(value, normalizedType, StringComparison.OrdinalIgnoreCase))
        {
            EditToolType = normalizedType;
            return;
        }

        if (string.Equals(normalizedType, "Round", StringComparison.OrdinalIgnoreCase))
        {
            EditLength = 0;
            EditWidth = 0;
        }
        else
        {
            EditDiameter = 0;
        }
        SetToolTypeFlags(normalizedType);
    }

    private void SetToolTypeFlags(string toolType)
    {
        IsRoundTool = string.Equals(toolType, "Round", StringComparison.OrdinalIgnoreCase);
        IsSquareTool = string.Equals(toolType, "Square", StringComparison.OrdinalIgnoreCase);
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
            Length = 0.0,
            Width = 0.0
        };
        Tools.Add(tool);
        SelectedTool = tool;
        StatusMessage = "New tool added. Fill in the details and save.";
        _dataStoreService.SaveToolRecords(Tools.ToList());
    }

    [RelayCommand]
    private void SaveTool()
    {
        if (SelectedTool is null) return;
        var idx = Tools.IndexOf(SelectedTool);
        if (idx < 0)
        {
            StatusMessage = "Selected tool not found.";
            return;
        }

        var normalizedType = NormalizeToolType(EditToolType);
        var updatedTool = new ToolRecord
        {
            ToolId = SelectedTool.ToolId,
            ToolName = EditToolName,
            ToolType = normalizedType,
            Diameter = normalizedType == "Round" ? EditDiameter : 0,
            Length = normalizedType == "Square" ? EditLength : 0,
            Width = normalizedType == "Square" ? EditWidth : 0,
            Notes = EditNotes,
            IsUsed = EditIsUsed
        };

        Tools[idx] = updatedTool;
        SelectedTool = updatedTool;
        IsStatusSuccess = true;
        StatusMessage = $"✓ Tool '{EditToolName}' saved successfully.";
        _dataStoreService.SaveToolRecords(Tools.ToList());

        // Clear edit fields after save
        EditToolId = 0;
        EditToolName = string.Empty;
        EditToolType = "Round";
        EditDiameter = 0;
        EditLength = 0;
        EditWidth = 0;
        EditNotes = string.Empty;
        EditIsUsed = false;
    }

    [RelayCommand]
    private void DeleteTool()
    {
        if (SelectedTool is null) return;
        Tools.Remove(SelectedTool);
        SelectedTool = null;
        IsStatusSuccess = false;
        StatusMessage = "Tool deleted.";
        _dataStoreService.SaveToolRecords(Tools.ToList());

        // Clear edit fields after delete
        EditToolId = 0;
        EditToolName = string.Empty;
        EditToolType = "Round";
        EditDiameter = 0;
        EditLength = 0;
        EditWidth = 0;
        EditNotes = string.Empty;
        EditIsUsed = false;
    }

    private static string NormalizeToolType(string? rawType)
    {
        var value = (rawType ?? string.Empty).Trim().ToLowerInvariant();

        if (value.StartsWith("sq") || value.Contains("squ") || value.Contains("seq") || value == "square")
            return "Square";

        return "Round";
    }

}

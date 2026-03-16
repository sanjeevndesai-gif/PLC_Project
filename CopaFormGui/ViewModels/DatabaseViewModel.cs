using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Models;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;

public partial class DatabaseViewModel : ObservableObject
{
    private readonly IControllerService _controllerService;

    [ObservableProperty]
    private ObservableCollection<ToolRecord> _toolRecords = new();

    [ObservableProperty]
    private ToolRecord? _selectedRecord;

    [ObservableProperty]
    private string _statusMessage = "Operator will Enter all the Data";

    [ObservableProperty]
    private string _notes = "Database Need to Save this file";

    [ObservableProperty]
    private bool _isConnected;

    public DatabaseViewModel(IControllerService controllerService)
    {
        _controllerService = controllerService;
        _controllerService.ConnectionStateChanged += (_, s) => IsConnected = s == ConnectionState.Connected;
        IsConnected = controllerService.IsConnected;
        LoadSampleData();
    }

    private void LoadSampleData()
    {
        ToolRecords = new ObservableCollection<ToolRecord>
        {
            new() { ToolId = 1, ToolName = "Round Punch 10mm",  ToolType = "Round",    Diameter = 10.0, Length = 50.0, StrokeLength = 30.0, MaxStrokes = 5000, CurrentStrokes = 1230, Status = "OK" },
            new() { ToolId = 2, ToolName = "Square Punch 8mm",  ToolType = "Square",   Diameter =  8.0, Length = 50.0, StrokeLength = 28.0, MaxStrokes = 4000, CurrentStrokes =  870, Status = "OK" },
            new() { ToolId = 3, ToolName = "Oblong 15x8mm",     ToolType = "Oblong",   Diameter = 15.0, Length = 55.0, StrokeLength = 32.0, MaxStrokes = 3000, CurrentStrokes = 2990, Status = "Warn" },
            new() { ToolId = 4, ToolName = "Round Punch 6mm",   ToolType = "Round",    Diameter =  6.0, Length = 45.0, StrokeLength = 25.0, MaxStrokes = 6000, CurrentStrokes =  350, Status = "OK" },
            new() { ToolId = 5, ToolName = "Hex Punch 12mm",    ToolType = "Hex",      Diameter = 12.0, Length = 52.0, StrokeLength = 30.0, MaxStrokes = 4500, CurrentStrokes = 4490, Status = "Warn" },
            new() { ToolId = 6, ToolName = "Triangular 10mm",   ToolType = "Triangle", Diameter = 10.0, Length = 50.0, StrokeLength = 28.0, MaxStrokes = 3500, CurrentStrokes =  110, Status = "OK" },
            new() { ToolId = 7, ToolName = "Round Punch 20mm",  ToolType = "Round",    Diameter = 20.0, Length = 60.0, StrokeLength = 35.0, MaxStrokes = 2000, CurrentStrokes =  760, Status = "OK" },
            new() { ToolId = 8, ToolName = "Square Punch 15mm", ToolType = "Square",   Diameter = 15.0, Length = 55.0, StrokeLength = 32.0, MaxStrokes = 2500, CurrentStrokes = 2510, Status = "Replace" },
        };
    }

    [RelayCommand]
    private void AddRecord()
    {
        var newRecord = new ToolRecord
        {
            ToolId = ToolRecords.Count + 1,
            ToolName = "New Tool",
            ToolType = "Round",
            Diameter = 10.0,
            Length = 50.0,
            StrokeLength = 30.0,
            MaxStrokes = 5000,
            CurrentStrokes = 0,
            Status = "OK"
        };
        ToolRecords.Add(newRecord);
        SelectedRecord = newRecord;
        StatusMessage = "New record added. Please fill in the details.";
    }

    [RelayCommand]
    private void DeleteRecord()
    {
        if (SelectedRecord != null)
        {
            ToolRecords.Remove(SelectedRecord);
            SelectedRecord = null;
            StatusMessage = "Record deleted.";
        }
    }

    [RelayCommand]
    private void SaveDatabase()
    {
        StatusMessage = "Database saved successfully.";
    }

    [RelayCommand]
    private void RefreshData()
    {
        LoadSampleData();
        StatusMessage = "Data refreshed.";
    }
}

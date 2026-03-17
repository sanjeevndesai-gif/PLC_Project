using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Models;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;

public partial class DatabaseViewModel : ObservableObject
{
    private readonly IControllerService _controllerService;
    private readonly IDataStoreService _dataStoreService;

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

    public DatabaseViewModel(IControllerService controllerService, IDataStoreService dataStoreService)
    {
        _controllerService = controllerService;
        _dataStoreService = dataStoreService;
        _controllerService.ConnectionStateChanged += (_, s) => IsConnected = s == ConnectionState.Connected;
        IsConnected = controllerService.IsConnected;
        LoadSampleData();
    }

    private void LoadSampleData()
    {
        var storedRecords = _dataStoreService.LoadToolRecords();
        if (storedRecords.Count > 0)
        {
            ToolRecords = new ObservableCollection<ToolRecord>(storedRecords);
            return;
        }

        ToolRecords = new ObservableCollection<ToolRecord>
        {
            new() { ToolId = 1, ToolName = "Round Punch 10mm",   ToolType = "Round",  Diameter = 10.0, Length = 0.0,  Width = 0.0 },
            new() { ToolId = 2, ToolName = "Square Punch 8x8",   ToolType = "Square", Diameter = 0.0,  Length = 8.0,  Width = 8.0 },
            new() { ToolId = 3, ToolName = "Round Punch 6mm",    ToolType = "Round",  Diameter = 6.0,  Length = 0.0,  Width = 0.0 },
            new() { ToolId = 4, ToolName = "Square Punch 12x10", ToolType = "Square", Diameter = 0.0,  Length = 12.0, Width = 10.0 },
        };

        _dataStoreService.SaveToolRecords(ToolRecords.ToList());
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
            Length = 0.0,
            Width = 0.0
        };
        ToolRecords.Add(newRecord);
        SelectedRecord = newRecord;
        StatusMessage = "New record added. Use only Round or Square tool type.";
        _dataStoreService.SaveToolRecords(ToolRecords.ToList());
    }

    [RelayCommand]
    private void DeleteRecord()
    {
        if (SelectedRecord != null)
        {
            ToolRecords.Remove(SelectedRecord);
            SelectedRecord = null;
            StatusMessage = "Record deleted.";
            _dataStoreService.SaveToolRecords(ToolRecords.ToList());
        }
    }

    [RelayCommand]
    private void SaveDatabase()
    {
        foreach (var record in ToolRecords)
        {
            var normalizedType = string.Equals(record.ToolType, "Square", StringComparison.OrdinalIgnoreCase)
                ? "Square"
                : "Round";

            record.ToolType = normalizedType;
            if (normalizedType == "Round")
            {
                record.Length = 0;
                record.Width = 0;
            }
            else
            {
                record.Diameter = 0;
            }
        }

        _dataStoreService.SaveToolRecords(ToolRecords.ToList());
        StatusMessage = "Database saved successfully. Shape rules applied.";
    }

    [RelayCommand]
    private void RefreshData()
    {
        LoadSampleData();
        StatusMessage = "Data refreshed.";
    }
}

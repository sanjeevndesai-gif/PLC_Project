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
    private ObservableCollection<PunchProgram> _programRecords = new();

    [ObservableProperty]
    private PunchProgram? _selectedRecord;

    [ObservableProperty]
    private string _statusMessage = "Saved punching records are shown here.";

    [ObservableProperty]
    private string _notes = "Database view shows Punching screen saved data only.";

    [ObservableProperty]
    private bool _isConnected;

    public DatabaseViewModel(IControllerService controllerService, IDataStoreService dataStoreService)
    {
        _controllerService = controllerService;
        _dataStoreService = dataStoreService;
        _controllerService.ConnectionStateChanged += (_, s) => IsConnected = s == ConnectionState.Connected;
        IsConnected = controllerService.IsConnected;
        LoadProgramData();
    }

    private void LoadProgramData()
    {
        var storedRecords = _dataStoreService.LoadPunchPrograms();
        ProgramRecords = new ObservableCollection<PunchProgram>(storedRecords);
    }

    public void ReloadData()
    {
        LoadProgramData();
        StatusMessage = "Data refreshed.";
    }

    [RelayCommand]
    private void AddRecord()
    {
        var newRecord = new PunchProgram
        {
            ProgramId = ProgramRecords.Count > 0 ? ProgramRecords.Max(p => p.ProgramId) + 1 : 1,
            ProgramName = "NEW_PROGRAM",
            Material = string.Empty,
            Comment = string.Empty,
            Length = 0,
            Width = 0,
            Thickness = 0,
            CreatedBy = "Operator"
        };
        ProgramRecords.Add(newRecord);
        SelectedRecord = newRecord;
        StatusMessage = "New punching record added.";
        _dataStoreService.SavePunchPrograms(ProgramRecords.ToList());
    }

    [RelayCommand]
    private void DeleteRecord()
    {
        if (SelectedRecord != null)
        {
            ProgramRecords.Remove(SelectedRecord);
            SelectedRecord = null;
            StatusMessage = "Record deleted.";
            _dataStoreService.SavePunchPrograms(ProgramRecords.ToList());
        }
    }

    [RelayCommand]
    private void SaveDatabase()
    {
        foreach (var record in ProgramRecords)
            record.ModifiedDate = DateTime.Now;

        _dataStoreService.SavePunchPrograms(ProgramRecords.ToList());
        StatusMessage = "Punching database saved successfully.";
    }

    [RelayCommand]
    private void RefreshData()
    {
        ReloadData();
    }

    [RelayCommand]
    private void DeleteOldData()
    {
        ProgramRecords.Clear();
        SelectedRecord = null;
        _dataStoreService.ClearPunchPrograms();
        LoadProgramData();
        StatusMessage = "All old database data deleted.";
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopaFormGui.Services;

namespace CopaFormGui.ViewModels;

public class ProductionEntry
{
    public string JobName { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public int Target { get; set; }
    public int Completed { get; set; }
    public int Rejected { get; set; }
    public DateTime ShiftStart { get; set; }
    public DateTime? ShiftEnd { get; set; }
    public string Operator { get; set; } = string.Empty;
    public int Net => Completed - Rejected;
    public double Efficiency => Target > 0 ? Math.Round((double)Net / Target * 100.0, 1) : 0.0;
    public string Duration => ShiftEnd.HasValue
        ? $"{(ShiftEnd.Value - ShiftStart):hh\\:mm}"
        : $"{(DateTime.Now - ShiftStart):hh\\:mm} (ongoing)";
}

public partial class ProductionViewModel : ObservableObject
{
    private readonly IControllerService _controllerService;

    [ObservableProperty] private bool _isConnected;

    // Current shift / active job counters
    [ObservableProperty] private string _currentJob = "PROG_001";
    [ObservableProperty] private string _currentPart = "Flange Pattern A";
    [ObservableProperty] private string _currentOperator = "Operator 1";

    [ObservableProperty] private int _targetCount = 200;
    [ObservableProperty] private int _completedCount = 134;
    [ObservableProperty] private int _rejectedCount = 3;
    [ObservableProperty] private int _strokesPerPart = 8;

    // Totals
    [ObservableProperty] private int _totalStrokesToday;
    [ObservableProperty] private int _totalPartsToday;
    [ObservableProperty] private int _totalRejectToday;

    [ObservableProperty] private double _efficiency;
    [ObservableProperty] private string _shiftStartDisplay = string.Empty;
    [ObservableProperty] private string _elapsedDisplay = string.Empty;
    [ObservableProperty] private string _statusMessage = "Shift in progress";

    [ObservableProperty]
    private ObservableCollection<ProductionEntry> _history = new();

    private DateTime _shiftStart;
    private System.Timers.Timer? _clockTimer;

    public int Remaining => Math.Max(0, TargetCount - CompletedCount);
    public double ProgressPct => TargetCount > 0
        ? Math.Min(100.0, CompletedCount * 100.0 / TargetCount) : 0.0;

    public ProductionViewModel(IControllerService controllerService)
    {
        _controllerService = controllerService;
        _controllerService.ConnectionStateChanged += (_, s) => IsConnected = s == ConnectionState.Connected;
        IsConnected = controllerService.IsConnected;

        _shiftStart = DateTime.Today.AddHours(8); // 08:00 start
        ShiftStartDisplay = _shiftStart.ToString("HH:mm  dd/MM/yyyy");

        LoadHistory();
        RecalcTotals();
        StartClock();
    }

    private void LoadHistory()
    {
        History = new ObservableCollection<ProductionEntry>
        {
            new() { JobName="PROG_003", PartName="Panel Pattern C",  Target=150, Completed=150, Rejected=1,
                    ShiftStart=DateTime.Today.AddDays(-1).AddHours(8),
                    ShiftEnd=DateTime.Today.AddDays(-1).AddHours(16), Operator="Operator 2" },
            new() { JobName="PROG_002", PartName="Bracket Pattern B", Target=100, Completed=97,  Rejected=2,
                    ShiftStart=DateTime.Today.AddDays(-1).AddHours(16),
                    ShiftEnd=DateTime.Today.AddDays(-1).AddHours(23), Operator="Operator 1" },
            new() { JobName="PROG_001", PartName="Flange Pattern A", Target=200, Completed=134, Rejected=3,
                    ShiftStart=DateTime.Today.AddHours(8), Operator="Operator 1" },
        };
    }

    private void RecalcTotals()
    {
        TotalStrokesToday = CompletedCount * StrokesPerPart;
        TotalPartsToday = CompletedCount;
        TotalRejectToday = RejectedCount;
        Efficiency = TargetCount > 0
            ? Math.Round((double)(CompletedCount - RejectedCount) / TargetCount * 100.0, 1)
            : 0.0;
        OnPropertyChanged(nameof(Remaining));
        OnPropertyChanged(nameof(ProgressPct));
    }

    private void StartClock()
    {
        _clockTimer = new System.Timers.Timer(1000);
        _clockTimer.Elapsed += (_, _) =>
        {
            ElapsedDisplay = $"{(DateTime.Now - _shiftStart):hh\\:mm\\:ss}";
        };
        _clockTimer.Start();
    }

    [RelayCommand]
    private void IncrementGood()
    {
        CompletedCount++;
        TotalStrokesToday += StrokesPerPart;
        RecalcTotals();
        StatusMessage = $"Part #{CompletedCount} counted.";
    }

    [RelayCommand]
    private void IncrementReject()
    {
        RejectedCount++;
        TotalRejectToday++;
        RecalcTotals();
        StatusMessage = $"Reject #{RejectedCount} recorded.";
    }

    [RelayCommand]
    private void ResetShift()
    {
        CompletedCount = 0;
        RejectedCount = 0;
        _shiftStart = DateTime.Now;
        ShiftStartDisplay = _shiftStart.ToString("HH:mm  dd/MM/yyyy");
        RecalcTotals();
        StatusMessage = "Shift counters reset.";
    }

    [RelayCommand]
    private void EndShift()
    {
        var entry = new ProductionEntry
        {
            JobName = CurrentJob,
            PartName = CurrentPart,
            Target = TargetCount,
            Completed = CompletedCount,
            Rejected = RejectedCount,
            ShiftStart = _shiftStart,
            ShiftEnd = DateTime.Now,
            Operator = CurrentOperator
        };
        History.Insert(0, entry);
        ResetShift();
        StatusMessage = "Shift ended and saved to history.";
    }
}

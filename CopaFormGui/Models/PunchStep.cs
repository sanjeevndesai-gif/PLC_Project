using System.ComponentModel;

namespace CopaFormGui.Models;

public class PunchStep : INotifyPropertyChanged
{
    public int StepNumber { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double M { get; set; }
    public double F { get; set; }
    private int _toolId;
    public int ToolId
    {
        get => _toolId;
        set
        {
            if (_toolId != value)
            {
                _toolId = value;
                OnPropertyChanged(nameof(ToolId));
                OnPropertyChanged(nameof(ToolInfo));
            }
        }
    }
    public string Operation { get; set; } = "Punch";
    public bool IsCompleted { get; set; }

    // Not persisted, for display only
    private string _toolInfo = string.Empty;
    public string ToolInfo
    {
        get => _toolInfo;
        set
        {
            if (_toolInfo != value)
            {
                _toolInfo = value;
                OnPropertyChanged(nameof(ToolInfo));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

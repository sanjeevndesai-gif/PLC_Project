namespace CopaFormGui.Models;

/// <summary>Represents a single PLC digital I/O point.</summary>
using System.ComponentModel;
using System.Runtime.CompilerServices;

public class IOPoint : INotifyPropertyChanged
{
    // Static event for output value changes (to be handled in ViewModel for actual PMAC call)
    public static event Action<IOPoint, string>? OutputValueChanged;

    private string _outputValue = string.Empty;
    public string OutputValue
    {
        get => _outputValue;
        set
        {
            if (_outputValue != value)
            {
                _outputValue = value;
                // Notify listeners (ViewModel) to send to PMAC
                if (IsOutput)
                    OutputValueChanged?.Invoke(this, value);
                OnPropertyChanged();
            }
        }
    }

    public int Address { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    private bool _state;
    public bool State
    {
        get => _state;
        set
        {
            if (_state != value)
            {
                _state = value;
                // For outputs, trigger OutputValueChanged event with string value ("1" or "0")
                if (IsOutput)
                    OutputValueChanged?.Invoke(this, value ? "1" : "0");
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    public bool IsOutput { get; set; }
}

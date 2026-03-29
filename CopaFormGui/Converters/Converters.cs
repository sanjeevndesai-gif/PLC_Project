using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace CopaFormGui.Converters;

[ValueConversion(typeof(bool), typeof(SolidColorBrush))]
public class BoolToConnectionColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b)
            ? new SolidColorBrush(Color.FromRgb(0x28, 0xA7, 0x45))   // Bootstrap green
            : new SolidColorBrush(Color.FromRgb(0xDC, 0x35, 0x45));  // Bootstrap red

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(bool), typeof(string))]
public class BoolToStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b) ? "ON" : "OFF";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(bool), typeof(Visibility))]
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(string), typeof(Visibility))]
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(bool), typeof(SolidColorBrush))]
public class BoolToErrorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b)
            ? new SolidColorBrush(Color.FromRgb(0xDC, 0x35, 0x45))  // red on error
            : new SolidColorBrush(Color.FromRgb(0x0D, 0x47, 0xA1)); // blue on success/info

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Converts a double value to bool by comparing it with a ConverterParameter double.</summary>
[ValueConversion(typeof(double), typeof(bool))]
public class EqualityToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d && parameter is string s &&
            double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double p))
            return Math.Abs(d - p) < 1e-9;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter is string s &&
            double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double p))
            return p;
        return System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>Converts an IOPointState to a SolidColorBrush.</summary>
[ValueConversion(typeof(bool), typeof(SolidColorBrush))]
public class IOStateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // If parameter is 'input', use green for ON (true), gray for OFF (false)
        if (parameter is string param && param == "input")
        {
            if (value is bool b && b)
                return new SolidColorBrush(Color.FromRgb(0x28, 0xA7, 0x45)); // Green for ON
            else
                return new SolidColorBrush(Color.FromRgb(0xBD, 0xBD, 0xBD)); // Gray for OFF
        }
        // Default: LED indicator (keep as before)
        return (value is bool b2 && b2)
            ? new SolidColorBrush(Color.FromRgb(0x28, 0xA7, 0x45))
            : new SolidColorBrush(Color.FromRgb(0xBD, 0xBD, 0xBD));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(CopaFormGui.Models.AlarmSeverity), typeof(SolidColorBrush))]
public class AlarmSeverityToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is CopaFormGui.Models.AlarmSeverity severity ? severity switch
        {
            CopaFormGui.Models.AlarmSeverity.Info     => new SolidColorBrush(Colors.DodgerBlue),
            CopaFormGui.Models.AlarmSeverity.Warning  => new SolidColorBrush(Colors.Orange),
            CopaFormGui.Models.AlarmSeverity.Error    => new SolidColorBrush(Colors.OrangeRed),
            CopaFormGui.Models.AlarmSeverity.Critical => new SolidColorBrush(Colors.Red),
            _ => new SolidColorBrush(Colors.Gray)
        } : new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

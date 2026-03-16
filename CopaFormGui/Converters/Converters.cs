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
        => (value is bool b && b) ? "Connected" : "Disconnected";

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

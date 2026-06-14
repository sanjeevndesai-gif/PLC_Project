using System;
using System.Globalization;
using System.Windows.Data;

namespace CopaFormGui.Converters
{
    public class SumConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                double sum = 0.0;
                foreach (var v in values)
                {
                    if (v == null) continue;
                    if (double.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                    {
                        sum += d;
                    }
                    else if (double.TryParse(v.ToString(), NumberStyles.Any, culture, out d))
                    {
                        sum += d;
                    }
                }
                return sum;
            }
            catch
            {
                return 0.0;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

using System;
using System.Globalization;
using System.Windows.Data;

namespace CopaFormGui.Views
{
    public class SheetEdgeCoordinateConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 4)
                return 0.0;

            double left = ToDouble(values[0]);
            double top = ToDouble(values[1]);
            double width = ToDouble(values[2]);
            double height = ToDouble(values[3]);

            string param = (parameter as string) ?? string.Empty;
            switch (param)
            {
                case "Left":
                    return left;
                case "Top":
                    return top;
                case "Right":
                    return left + width;
                case "Bottom":
                    return top + height;
                default:
                    return 0.0;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private double ToDouble(object o)
        {
            if (o == null)
                return 0.0;
            if (o is double d)
                return d;
            if (double.TryParse(o.ToString(), out d))
                return d;
            return 0.0;
        }
    }
}

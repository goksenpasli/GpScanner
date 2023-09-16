using System;
using System.Globalization;
using System.Windows.Data;

namespace TwainControl.Converter;

public sealed class DivideConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double or int)
        {
            double inputValue = System.Convert.ToDouble(value);
            double factor = System.Convert.ToDouble(parameter, CultureInfo.InvariantCulture);
            return inputValue / factor;
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) { throw new NotImplementedException(); }
}
using System;
using System.Globalization;
using System.Windows.Data;

namespace Extensions;

public class EnumToIntValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) { return value?.GetType().IsEnum != true ? null : (int)value; }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) { throw new NotImplementedException(); }
}
using System;
using System.Globalization;
using System.Windows.Data;

namespace Extensions;

public sealed class TimeSpanToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => ((TimeSpan)value).ToString(@"hh\:mm\:ss");

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => TimeSpan.Parse((string)value);
}
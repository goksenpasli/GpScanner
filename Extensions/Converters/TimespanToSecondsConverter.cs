using System;
using System.Globalization;
using System.Windows.Data;

namespace Extensions;

public sealed class TimespanToSecondsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        TimeSpan timeSpan = (TimeSpan)value;
        return timeSpan.TotalSeconds;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    { return TimeSpan.FromSeconds((double)value); }
}
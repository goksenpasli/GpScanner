using System;
using System.Globalization;
using System.Windows.Data;

namespace Extensions
{
    public sealed class TimespanToTickConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            TimeSpan timeSpan = (TimeSpan)value;
            return timeSpan.Ticks;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return TimeSpan.FromTicks((long)value);
        }
    }
}
using System;
using System.Globalization;
using System.Windows.Data;

namespace TwainControl.Converter
{
    public sealed class SplitConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is string profile ? profile.Split(',')[0] : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
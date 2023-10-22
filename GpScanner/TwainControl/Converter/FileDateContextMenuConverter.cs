using System;
using System.Globalization;
using System.Windows.Data;

namespace TwainControl.Converter
{
    public sealed class FileDateContextMenuConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string placeholder)
            {
                _ = Scanner.FileContextMenuDictionary.TryGetValue(placeholder, out string result);
                return result;
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
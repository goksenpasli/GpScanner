using System;
using System.Globalization;
using System.Windows.Data;

namespace GpScanner.Converter
{
    public sealed class StringToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is string stringValue && int.TryParse(stringValue, out int result) ? result : 0;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}

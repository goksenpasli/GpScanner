using System;
using System.Globalization;
using System.Windows.Data;

namespace GpScanner.Converter
{
    public sealed class PageSizePreviewWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) { return value is int widthratio ? widthratio * 210 / 100 : 0d; }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) { throw new NotImplementedException(); }
    }
}

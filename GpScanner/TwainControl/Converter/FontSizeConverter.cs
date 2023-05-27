using System;
using System.Globalization;
using System.Windows.Data;

namespace TwainControl.Converter;

public sealed class FontSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    { return value is double size ? size / 5.5 : 0; }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    { throw new NotImplementedException(); }
}
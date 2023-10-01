using PdfSharp.Drawing;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TwainControl.Converter;

public sealed class XKnownColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is XKnownColor xKnownColor
        ? XColor.FromKnownColor(xKnownColor).ToWpfColor()
        : Colors.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
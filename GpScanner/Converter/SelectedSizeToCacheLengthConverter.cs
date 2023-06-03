using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GpScanner.Converter;

public sealed class SelectedSizeToCacheLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    { return value is Size selectedsize ? (int)(SystemParameters.PrimaryScreenWidth * SystemParameters.PrimaryScreenHeight / selectedsize.Height / selectedsize.Width / 2) : 4; }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) { throw new NotImplementedException(); }
}
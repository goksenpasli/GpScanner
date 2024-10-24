using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace TwainControl.Converter;

public sealed class FontSizeConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) => values[0] is double size && values[1] is BitmapSource bitmapsource && values[2] is Grid grid
                                                                                                      ? bitmapsource.Width > bitmapsource.Height ? grid.ActualWidth / bitmapsource.Width * size : grid.ActualHeight / bitmapsource.Height * size
                                                                                                      : 25d;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
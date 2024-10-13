using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;

namespace TwainControl.Converter;

public sealed class RandomColorBrushConverter : IValueConverter
{
    private static readonly Random RandomGenerator = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        Color randomColor;
        do
        {
            byte r = (byte)RandomGenerator.Next(256);
            byte g = (byte)RandomGenerator.Next(256);
            byte b = (byte)RandomGenerator.Next(256);
            randomColor = Color.FromRgb(r, g, b);
        }
        while (parameter is IEnumerable<Color> excludedColors && excludedColors.Contains(randomColor));
        SolidColorBrush solidColorBrush = new(randomColor);
        solidColorBrush.Freeze();
        return solidColorBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
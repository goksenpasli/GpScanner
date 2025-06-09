using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TwainControl.Converter;

public class StringToBrushConverter : IValueConverter
{
    private static readonly BrushConverter _brushConverter = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            try
            {
                return _brushConverter.ConvertFromString(s) as Brush ?? Brushes.Black;
            }
            catch
            {
            }
        }
        return Brushes.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is SolidColorBrush scb ? scb.Color.ToString() : "#FF000000";
}

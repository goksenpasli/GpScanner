using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace TwainControl.Converter;

public sealed class Jb2ToBitmapImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string filepath && File.Exists(filepath))
        {
            Jb2FileHandler jb2FileHandler = new();
            return jb2FileHandler.LoadImageAsync(filepath);
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
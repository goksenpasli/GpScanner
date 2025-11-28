using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace TwainControl.Converter;

public sealed class J2kToBitmapImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string filepath && File.Exists(filepath))
        {
            J2kFileHandler j2kFileHandler = new();
            return j2kFileHandler.LoadImageAsync(filepath);
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
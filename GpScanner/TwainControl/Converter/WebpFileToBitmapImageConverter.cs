using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace TwainControl.Converter;

public sealed class WebpFileToBitmapImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string filepath && File.Exists(filepath) && WebPWrapper.WebP.WebpDllExists)
        {
            WebpFileHandler fileHandler = new();
            return fileHandler.LoadWebpImage(0, filepath);
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
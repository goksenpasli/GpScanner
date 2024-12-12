using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using WebPWrapper;

namespace TwainControl.Converter;

public sealed class WebpFileToBitmapImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string filepath && File.Exists(filepath) && WebP.WebpDllExists)
        {
            WebpFileHandler fileHandler = new();
            return parameter is string converterparameter && int.TryParse(converterparameter, out int decodeheight) ? fileHandler.LoadWebpImage(decodeheight, filepath, false) : fileHandler.LoadWebpImage(0, filepath);
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
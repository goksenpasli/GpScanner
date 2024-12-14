using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using WebPWrapper;

namespace TwainControl.Converter;

public sealed class WebpFileToBitmapImageConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values[0] is string filepath && File.Exists(filepath) && WebP.WebpDllExists && values[1] is double decodeheight)
        {
            WebpFileHandler fileHandler = new();
            return decodeheight != 0d ? fileHandler.LoadWebpImage((int)decodeheight, filepath, false) : fileHandler.LoadWebpImage(0, filepath);

        }
        return null;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
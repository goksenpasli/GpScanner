using System;
using System.Globalization;
using System.Windows.Data;

namespace TwainControl.Converter;

public sealed class XpsFileToBitmapImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string filepath)
        {
            XpsFileHandler fileHandler = new();
            return fileHandler.LoadXpsSinglePagesAsync(filepath);
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
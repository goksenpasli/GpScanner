using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Ink;

namespace TwainControl.Converter;

public sealed class Base64StringToStrokeCollectionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string base64strokedata)
        {
            byte[] data = System.Convert.FromBase64String(base64strokedata);
            using MemoryStream ms = new(data);
            data = null;
            return (StrokeCollection)new(ms);
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
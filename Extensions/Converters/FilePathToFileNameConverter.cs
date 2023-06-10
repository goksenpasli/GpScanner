using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace Extensions;

public sealed class FilePathToFileNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            return Path.GetFileNameWithoutExtension(value as string);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
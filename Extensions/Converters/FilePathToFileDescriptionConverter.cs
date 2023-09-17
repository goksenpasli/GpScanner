using System;
using System.Globalization;
using System.Windows.Data;
using static Extensions.ExtensionMethods;

namespace Extensions;

public sealed class FilePathToFileDescriptionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            return (value as string).GetFileType(new SHFILEINFO());
        }
        catch (Exception)
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
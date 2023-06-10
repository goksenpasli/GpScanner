using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace Extensions;

public sealed class SystemFileIconConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values[0] is string systemfilename && File.Exists($@"{Environment.SystemDirectory}\{systemfilename}") && values[1] is string index)
        {
            try
            {
                return $@"{Environment.SystemDirectory}\{systemfilename}".IconCreate(System.Convert.ToInt32(index));
            }
            catch (Exception)
            {
                return null;
            }
        }

        return null;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
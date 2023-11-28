using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Data;

namespace Extensions
{
    public sealed class GetDirectoriesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                return value is string path ? Directory.GetDirectories(path) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => !(bool)value;
    }
}
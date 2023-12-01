using System;
using System.Globalization;
using System.Windows.Data;

namespace Extensions
{
    public sealed class FolderPathToLocalizedNameConverter :  IValueConverter
    {
        public  object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                return value is string path ? ShellIcon.GetDisplayName(path) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public  object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
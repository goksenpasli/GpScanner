using System;
using System.Globalization;
using System.Windows.Data;

namespace Extensions
{
    public sealed class FolderPathToIconConverter : FilePathToIconConverter, IValueConverter
    {
        public new object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                return value is string path ? ShellIcon.GetFolderIconBySize(path, IconSize) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public new object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace Extensions
{
    public sealed class GetDirectoriesConverter : DependencyObject, IValueConverter
    {
        public static readonly DependencyProperty ShowHiddenFoldersProperty =
            DependencyProperty.Register("ShowHiddenFolders", typeof(bool), typeof(GetDirectoriesConverter), new PropertyMetadata(false));

        public bool ShowHiddenFolders { get => (bool)GetValue(ShowHiddenFoldersProperty); set => SetValue(ShowHiddenFoldersProperty, value); }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is string path)
                {
                    return ShowHiddenFolders
                           ? Directory.GetDirectories(path)
                           : Directory.GetDirectories(path).Where(z => !new DirectoryInfo(z).Attributes.HasFlag(FileAttributes.Hidden));
                }
            }
            catch (Exception)
            {
                return null;
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => !(bool)value;
    }
}
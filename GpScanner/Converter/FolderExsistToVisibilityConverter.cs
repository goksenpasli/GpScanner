using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;

namespace GpScanner.Converter
{
    public sealed class FolderExsistToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        { return DesignerProperties.GetIsInDesignMode(new DependencyObject()) ? Visibility.Visible : value is string folder ? Directory.Exists(folder) ? Visibility.Visible : Visibility.Collapsed : Visibility.Collapsed; }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) { throw new NotImplementedException(); }
    }
}

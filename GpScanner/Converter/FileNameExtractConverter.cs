using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace GpScanner.Converter
{
    public sealed class FileNameExtractConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !DesignerProperties.GetIsInDesignMode(new DependencyObject()) && value is string filename && !string.IsNullOrEmpty(filename)
                ? Path.GetFileName(filename)
                : (object)null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value as BitmapSource;
        }
    }
}
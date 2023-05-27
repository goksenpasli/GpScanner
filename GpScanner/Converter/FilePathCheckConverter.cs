using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;

namespace GpScanner.Converter;

public sealed class FilePathCheckConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return !DesignerProperties.GetIsInDesignMode(new DependencyObject()) &&
                value is string filename &&
                File.Exists(filename)
            ? filename
            : null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    { throw new NotImplementedException(); }
}
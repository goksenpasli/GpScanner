﻿using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace GpScanner.Converter
{
    public sealed class FilePathMergeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !DesignerProperties.GetIsInDesignMode(new DependencyObject()) && value is string filename && !string.IsNullOrEmpty(filename)
                ? filename
                : (object)null;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value as BitmapSource;
    }
}
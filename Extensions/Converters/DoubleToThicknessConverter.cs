using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Extensions;

public sealed class DoubleToThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) { return value is Thickness thickness ? thickness.Left : 0; }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) { return value is double margin ? new Thickness(margin) : new Thickness(0); }
}
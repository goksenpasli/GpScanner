using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Extensions;

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is null ? parameter is null ? Visibility.Collapsed : Visibility.Visible : (object)(parameter is null ? Visibility.Visible : Visibility.Collapsed);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
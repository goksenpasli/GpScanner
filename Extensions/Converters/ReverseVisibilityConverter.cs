using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Extensions;

public sealed class ReverseVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is Visibility visibility ? visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Convert(value, targetType, parameter, culture);
}
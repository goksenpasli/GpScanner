﻿using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Extensions;

public sealed class VisibilityToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is Visibility visibility && visibility == Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is bool x && x ? Visibility.Visible : Visibility.Collapsed;
}
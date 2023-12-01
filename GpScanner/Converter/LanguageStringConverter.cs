using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TwainControl;

namespace GpScanner.Converter;

public sealed class LanguageStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !DesignerProperties.GetIsInDesignMode(new DependencyObject()) &&
    value is string langresource
                                                                                                   ? Translation.GetResStringValue(langresource)
                                                                                                   : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
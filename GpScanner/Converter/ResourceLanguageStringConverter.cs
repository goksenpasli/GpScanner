using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TwainControl;

namespace GpScanner.Converter;

public sealed class ResourceLanguageStringConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) => !DesignerProperties.GetIsInDesignMode(new DependencyObject()) && values[0] is string langresource
                                                                                                      ? Translation.GetResStringValue(langresource)
                                                                                                      : string.Empty;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
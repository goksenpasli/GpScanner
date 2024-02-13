using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TwainControl.Converter;

public class CentimetersToInchesConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        return values[0] is double centimeters && values[1] is CultureInfo cultureInfo
               ? centimeters == 0 ? Translation.GetResStringValue("ORİGİNAL") : cultureInfo.Name == "en-US" ? $"{centimeters / 2.54:N2} in" : $"{centimeters:N2} cm"
               : DependencyProperty.UnsetValue;
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
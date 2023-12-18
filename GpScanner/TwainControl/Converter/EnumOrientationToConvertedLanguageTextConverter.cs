using System;
using System.Globalization;
using System.Windows.Data;
using TwainWpf.TwainNative;

namespace TwainControl.Converter;

public sealed class EnumOrientationToConvertedLanguageTextConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        return values[0] switch
        {
            Orientation orientation => Translation.GetResStringValue(Enum.GetName(typeof(Orientation), orientation)),
            _ => string.Empty
        };
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

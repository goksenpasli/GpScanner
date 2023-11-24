using System;
using System.Globalization;
using System.Windows.Data;
using TwainWpf.TwainNative;

namespace TwainControl.Converter;

public sealed class EnumOrientationToConvertedLanguageTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            Orientation orientation => Translation.GetResStringValue(Enum.GetName(typeof(Orientation), orientation)),
            _ => string.Empty
        };
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

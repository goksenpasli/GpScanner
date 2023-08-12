using System;
using System.Globalization;
using System.Windows.Data;

namespace TwainControl.Converter;

public sealed class EnumPageRotationToConvertedLanguageTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            PageRotation rotation => Translation.GetResStringValue(Enum.GetName(typeof(PageRotation), rotation)),
            _ => string.Empty
        };
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) { throw new NotImplementedException(); }
}
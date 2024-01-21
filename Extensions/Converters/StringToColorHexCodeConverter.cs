using System;
using System.Globalization;
using System.Windows.Data;

namespace Extensions;

public sealed class StringToColorHexCodeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is string color ? ColorPicker.ConvertColorNameToHex(color) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
using System;
using System.Globalization;
using System.Windows.Data;

namespace Extensions;

public sealed class NullableToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => parameter is not null ? value is null : value is not null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
using System;
using System.Globalization;
using System.Windows.Data;
using TwainControl;

namespace GpScanner.Converter;

public sealed class IntToMonthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is int monthNumber && monthNumber >= 1 && monthNumber <= 12
                                                                                                   ? new DateTime(1, monthNumber, 1).ToString("MMMM")
                                                                                                   : Translation.GetResStringValue("ERROR");

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
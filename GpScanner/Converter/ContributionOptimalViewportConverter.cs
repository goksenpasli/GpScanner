using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace GpScanner.Converter;

public sealed class ContributionOptimalViewportConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) => values[0] is IEnumerable<string> name && values[1] is double size ? name.Take((int)(800 * 800 / size / size)).ToList() : null;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
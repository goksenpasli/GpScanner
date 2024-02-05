using Extensions;
using GpScanner.ViewModel;
using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace GpScanner.Converter;

public sealed class MonthContributionDataFileNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is IGrouping<int, ContributionData> data
                                                                                                   ? data.Where(z => z.Count > 0).SelectMany(z => ((ExtendedContributionData)z).Name).ToList()
                                                                                                   : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
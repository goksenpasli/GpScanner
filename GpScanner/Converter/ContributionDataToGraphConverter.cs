using Extensions;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;

namespace GpScanner.Converter;

public sealed class ContributionDataToGraphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ObservableCollection<ContributionData> contributions)
        {
            ObservableCollection<Chart> charts = [];
            foreach (ContributionData data in contributions)
            {
                charts.Add(new Chart() { ChartBrush = data.Stroke, ChartValue = data.Count });
            }
            return charts;
        }
        else
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
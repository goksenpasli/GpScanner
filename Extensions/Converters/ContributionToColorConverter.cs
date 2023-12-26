using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Extensions;

public sealed class ContributionToColorConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values[0] is int contributionCount && values[1] is int maxcontribution && values[2] is Color contributioncolor && values[3] is Color zerocontributioncolor)
        {
            int alpha = (int)(255 * ((double)contributionCount / maxcontribution));
            if (alpha < 0)
            {
                alpha = 0;
            }

            if (alpha > 255)
            {
                alpha = 255;
            }
            SolidColorBrush sb;
            if (contributionCount == 0)
            {
                sb = new SolidColorBrush(zerocontributioncolor);
                sb.Freeze();
                return sb;
            }
            contributioncolor.A = (byte)alpha;
            sb = new(contributioncolor);
            sb.Freeze();
            return sb;
        }

        return new SolidColorBrush(Colors.Transparent);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
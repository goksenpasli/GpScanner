using System;
using System.Globalization;
using System.Windows.Data;
using TwainWpf.TwainNative;

namespace TwainControl.Converter
{
    public sealed class EnumOrientationToTwainVersionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Orientation orientation)
            {
                return Enum.GetName(typeof(Orientation), orientation) switch
                {
                    "Auto" or "AutoText" or "AutoPicture" => "Twain 2.0",
                    _ => string.Empty
                };
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
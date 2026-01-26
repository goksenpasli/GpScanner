using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TwainControl.Converter;

public sealed class NumberRangeToColorConverter : DependencyObject, IValueConverter
{
    public static readonly DependencyProperty ColorsProperty = DependencyProperty.Register(
        "Colors",
        typeof(Color[]),
        typeof(NumberRangeToColorConverter),
        new PropertyMetadata(new Color[] { System.Windows.Media.Colors.Lime, System.Windows.Media.Colors.Orange, System.Windows.Media.Colors.Red }));
    public static readonly DependencyProperty MaxNumberProperty = DependencyProperty.Register("MaxNumber", typeof(int), typeof(NumberRangeToColorConverter), new PropertyMetadata(100));
    public static readonly DependencyProperty MinNumberProperty = DependencyProperty.Register("MinNumber", typeof(int), typeof(NumberRangeToColorConverter), new PropertyMetadata(0));
    private static readonly SolidColorBrush BlackBrush;
    public static readonly DependencyProperty ReverseColorsProperty = DependencyProperty.Register("ReverseColors", typeof(bool), typeof(NumberRangeToColorConverter), new PropertyMetadata(false, ColorReverseCallBack));

    static NumberRangeToColorConverter()
    {
        BlackBrush = new SolidColorBrush(System.Windows.Media.Colors.Black);
        BlackBrush.Freeze();
    }

    public Color[] Colors { get => (Color[])GetValue(ColorsProperty); set => SetValue(ColorsProperty, value); }

    public int MaxNumber { get => (int)GetValue(MaxNumberProperty); set => SetValue(MaxNumberProperty, value); }

    public int MinNumber { get => (int)GetValue(MinNumberProperty); set => SetValue(MinNumberProperty, value); }

    public bool ReverseColors { get => (bool)GetValue(ReverseColorsProperty); set => SetValue(ReverseColorsProperty, value); }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (Colors == null || Colors.Length == 0)
        {
            return DependencyProperty.UnsetValue;
        }

        if (value is double or int or string)
        {
            if (value is string strValue)
            {
                value = double.TryParse(strValue, NumberStyles.Any, culture, out double res) ? res : (object)0;
            }

            int normalizedNumber = Math.Max(MinNumber, Math.Min(MaxNumber, System.Convert.ToInt32(value)));
            int rangeCount = Colors.Length;
            double rangeSize = (MaxNumber - MinNumber + 1) / (double)rangeCount;
            int colorIndex = Math.Min(rangeCount - 1, (int)((normalizedNumber - MinNumber) / rangeSize));
            SolidColorBrush brush = new(Colors[colorIndex]);
            brush?.Freeze();
            return brush;
        }

        return BlackBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();

    private static void ColorReverseCallBack(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NumberRangeToColorConverter numberRangeToColorConverter && (bool)e.NewValue)
        {
            numberRangeToColorConverter.Colors = [ .. numberRangeToColorConverter.Colors.Reverse() ];
        }
    }
}
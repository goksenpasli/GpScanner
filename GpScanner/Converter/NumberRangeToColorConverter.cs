using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace GpScanner.Converter
{
    public sealed class NumberRangeToColorConverter : DependencyObject, IValueConverter
    {
        public static readonly DependencyProperty ColorsProperty = DependencyProperty.Register(
            "Colors",
            typeof(Color[]),
            typeof(NumberRangeToColorConverter),
            new PropertyMetadata(new Color[] { System.Windows.Media.Colors.Lime, System.Windows.Media.Colors.Yellow, System.Windows.Media.Colors.Red }));
        public static readonly DependencyProperty MaxNumberProperty = DependencyProperty.Register("MaxNumber", typeof(int), typeof(NumberRangeToColorConverter), new PropertyMetadata(100));
        public static readonly DependencyProperty MinNumberProperty = DependencyProperty.Register("MinNumber", typeof(int), typeof(NumberRangeToColorConverter), new PropertyMetadata(0));
        public static readonly DependencyProperty ReverseColorsProperty = DependencyProperty.Register(
            "ReverseColors",
            typeof(bool),
            typeof(NumberRangeToColorConverter),
            new PropertyMetadata(false, ColorReverseCallBack));

        public Color[] Colors { get => (Color[])GetValue(ColorsProperty); set => SetValue(ColorsProperty, value); }

        public int MaxNumber { get => (int)GetValue(MaxNumberProperty); set => SetValue(MaxNumberProperty, value); }

        public int MinNumber { get => (int)GetValue(MinNumberProperty); set => SetValue(MinNumberProperty, value); }

        public bool ReverseColors { get => (bool)GetValue(ReverseColorsProperty); set => SetValue(ReverseColorsProperty, value); }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int number)
            {
                int normalizedNumber = Math.Max(MinNumber, Math.Min(MaxNumber, number));
                int rangeCount = Colors.Length;
                double rangeSize = (MaxNumber - MinNumber + 1) / (double)rangeCount;
                int colorIndex = (int)((normalizedNumber - MinNumber) / rangeSize);
                SolidColorBrush brush = new(Color.FromArgb(Colors[colorIndex].A, Colors[colorIndex].R, Colors[colorIndex].G, Colors[colorIndex].B));
                brush.Freeze();
                return brush;
            }

            return new SolidColorBrush(System.Windows.Media.Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) { throw new NotImplementedException(); }

        private static void ColorReverseCallBack(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NumberRangeToColorConverter numberRangeToColorConverter && (bool)e.NewValue)
            {
                numberRangeToColorConverter.Colors = numberRangeToColorConverter.Colors.Reverse().ToArray();
            }
        }
    }
}

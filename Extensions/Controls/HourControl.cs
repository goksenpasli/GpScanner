using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Extensions
{
    public class HourControl : Control
    {
        public static readonly DependencyProperty HourProperty = DependencyProperty.Register("Hour", typeof(string), typeof(HourControl), new PropertyMetadata(string.Empty, Changed));

        public static readonly DependencyProperty HourValueProperty = DependencyProperty.Register("HourValue", typeof(TimeSpan), typeof(HourControl), new PropertyMetadata(null));

        static HourControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(HourControl), new FrameworkPropertyMetadata(typeof(HourControl)));
        }

        public string Hour
        {
            get => (string)GetValue(HourProperty);
            set => SetValue(HourProperty, value);
        }

        public TimeSpan HourValue
        {
            get => (TimeSpan)GetValue(HourValueProperty);
            set => SetValue(HourValueProperty, value);
        }

        private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HourControl hourControl && DateTime.TryParseExact(hourControl.Hour, "H:m", new CultureInfo("tr-TR"), DateTimeStyles.None, out DateTime saat))
            {
                hourControl.HourValue = saat.TimeOfDay;
            }
        }
    }
}
using System.Windows;
using System.Windows.Controls;

namespace Extensions
{
    public static class ProgressBarExtensions
    {
        public static readonly DependencyProperty IsCenteredProperty = DependencyProperty.RegisterAttached("IsCentered", typeof(bool), typeof(ProgressBarExtensions), new PropertyMetadata(false, OnIsCenteredChanged));

        public static bool GetIsCentered(this DependencyObject obj) => (bool)obj.GetValue(IsCenteredProperty);

        public static void SetIsCentered(this DependencyObject obj, bool value) => obj.SetValue(IsCenteredProperty, value);

        private static void OnIsCenteredChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ProgressBar progressBar)
            {
                if ((bool)e.NewValue)
                {
                    progressBar.ValueChanged += ProgressBar_ValueChanged;
                    progressBar.Loaded += ProgressBar_Loaded;
                }
                else
                {
                    progressBar.ValueChanged -= ProgressBar_ValueChanged;
                    progressBar.Loaded -= ProgressBar_Loaded;
                }
            }
        }

        private static void ProgressBar_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ProgressBar progressBar && progressBar.Template?.FindName("PART_Indicator", progressBar) is FrameworkElement indicator)
            {
                indicator.HorizontalAlignment = HorizontalAlignment.Center;
            }
        }

        private static void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is ProgressBar progressBar && progressBar.Template?.FindName("PART_Indicator", progressBar) is FrameworkElement indicator)
            {
                indicator.Width = progressBar.ActualWidth * e.NewValue / 2;
            }
        }
    }
}

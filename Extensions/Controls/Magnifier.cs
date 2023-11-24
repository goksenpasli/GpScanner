using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Extensions
{
    public class Magnifier : Canvas
    {
        public static readonly DependencyProperty ContentPanelProperty = DependencyProperty.Register(nameof(ContentPanel), typeof(UIElement), typeof(Magnifier), new PropertyMetadata(default(UIElement)));
        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(Magnifier), new PropertyMetadata(true));
        public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(nameof(Radius), typeof(double), typeof(Magnifier), new PropertyMetadata(50d));
        public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(nameof(Stroke), typeof(SolidColorBrush), typeof(Magnifier), new PropertyMetadata(Brushes.Cyan));
        public static readonly DependencyProperty ZoomFactorProperty = DependencyProperty.Register(nameof(ZoomFactor), typeof(double), typeof(Magnifier), new PropertyMetadata(3d));

        public UIElement ContentPanel { get => (UIElement)GetValue(ContentPanelProperty); set => SetValue(ContentPanelProperty, value); }

        public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

        public double Radius { get => (double)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

        public SolidColorBrush Stroke { get => (SolidColorBrush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

        public double ZoomFactor { get => (double)GetValue(ZoomFactorProperty); set => SetValue(ZoomFactorProperty, value); }

        private VisualBrush MagnifierBrush { get; set; }

        private Ellipse MagnifierCircle { get; set; }

        private Canvas MagnifierPanel { get; } = new Canvas { IsHitTestVisible = false };

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (ContentPanel is not null && (e.Property == ContentPanelProperty) && (VisualTreeHelper.GetParent(ContentPanel) is Panel container))
            {
                MagnifierBrush = new VisualBrush(ContentPanel) { ViewboxUnits = BrushMappingMode.Absolute };
                MagnifierCircle = new Ellipse { Stroke = Stroke, Width = 2 * Radius, Height = 2 * Radius, Visibility = Visibility.Hidden, Fill = MagnifierBrush };
                _ = MagnifierPanel.Children.Add(MagnifierCircle);
                _ = container.Children.Add(MagnifierPanel);
                ContentPanel.MouseEnter += ContentPanel_MouseEnter;
                ContentPanel.MouseLeave += ContentPanel_MouseLeave;
                ContentPanel.MouseMove += ContentPanelOnMouseMove;
            }

            if (MagnifierCircle is not null)
            {
                if (e.Property == RadiusProperty)
                {
                    MagnifierCircle.Width = MagnifierCircle.Height = 2 * Radius;
                }
                if (e.Property == StrokeProperty)
                {
                    MagnifierCircle.Stroke = Stroke;
                }
            }
        }

        private void ContentPanel_MouseEnter(object sender, MouseEventArgs e) => MagnifierCircle.Visibility = !IsActive ? Visibility.Collapsed : Visibility.Visible;

        private void ContentPanel_MouseLeave(object sender, MouseEventArgs e) => MagnifierCircle.Visibility = Visibility.Collapsed;

        private void ContentPanelOnMouseMove(object sender, MouseEventArgs e)
        {
            Point center = e.GetPosition(ContentPanel);
            double length = MagnifierCircle.ActualWidth * (1 / ZoomFactor);
            double radius = length / 2;
            SetViewBox(new Rect(center.X - radius, center.Y - radius, length, length));
            MagnifierCircle?.SetValue(LeftProperty, center.X - (MagnifierCircle.ActualWidth / 2));
            MagnifierCircle?.SetValue(TopProperty, center.Y - (MagnifierCircle.ActualHeight / 2));
        }

        private void SetViewBox(Rect value) => MagnifierBrush.Viewbox = value;
    }
}

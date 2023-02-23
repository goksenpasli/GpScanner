using System.Windows;
using System.Windows.Controls.Primitives;

namespace Extensions
{
    public class ContentToggleButton : ToggleButton
    {
        public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register("CornerRadius", typeof(CornerRadius), typeof(ContentToggleButton), new PropertyMetadata(new CornerRadius(0d)));

        public static readonly DependencyProperty OverContentProperty = DependencyProperty.Register("OverContent", typeof(object), typeof(ContentToggleButton), new PropertyMetadata(null));

        public static readonly DependencyProperty PlacementModeProperty = DependencyProperty.Register("PlacementMode", typeof(PlacementMode), typeof(ContentToggleButton), new PropertyMetadata(PlacementMode.Bottom));

        public static readonly DependencyProperty StaysOpenProperty = DependencyProperty.Register("StaysOpen", typeof(bool), typeof(ContentToggleButton), new PropertyMetadata(false));

        static ContentToggleButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ContentToggleButton), new FrameworkPropertyMetadata(typeof(ContentToggleButton)));
        }

        public CornerRadius CornerRadius {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public object OverContent {
            get => GetValue(OverContentProperty);
            set => SetValue(OverContentProperty, value);
        }

        public PlacementMode PlacementMode {
            get => (PlacementMode)GetValue(PlacementModeProperty);
            set => SetValue(PlacementModeProperty, value);
        }

        public bool StaysOpen {
            get => (bool)GetValue(StaysOpenProperty);
            set => SetValue(StaysOpenProperty, value);
        }

        public override string ToString()
        {
            return Content.ToString();
        }
    }
}
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Extensions
{
    public class SplitButton : Button
    {
        public static readonly DependencyProperty InternalContentProperty = DependencyProperty.Register("InternalContent", typeof(object), typeof(SplitButton), new PropertyMetadata(null));

        public static readonly DependencyProperty IsSplitPartOpenProperty = DependencyProperty.Register("IsSplitPartOpen", typeof(bool), typeof(SplitButton), new PropertyMetadata(false));

        public static readonly DependencyProperty PlacementModeProperty = DependencyProperty.Register("PlacementMode", typeof(PlacementMode), typeof(SplitButton), new PropertyMetadata(PlacementMode.Bottom));

        public static readonly DependencyProperty SplitContentPartIsEnabledProperty = DependencyProperty.Register("SplitContentPartIsEnabled", typeof(bool), typeof(SplitButton), new PropertyMetadata(true));

        public static readonly DependencyProperty StayOpenProperty = DependencyProperty.Register("StayOpen", typeof(bool), typeof(SplitButton), new PropertyMetadata(false));

        static SplitButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SplitButton), new FrameworkPropertyMetadata(typeof(SplitButton)));
        }

        public object InternalContent {
            get => GetValue(InternalContentProperty);
            set => SetValue(InternalContentProperty, value);
        }

        public bool IsSplitPartOpen {
            get => (bool)GetValue(IsSplitPartOpenProperty);
            set => SetValue(IsSplitPartOpenProperty, value);
        }

        public PlacementMode PlacementMode {
            get => (PlacementMode)GetValue(PlacementModeProperty);
            set => SetValue(PlacementModeProperty, value);
        }

        public bool SplitContentPartIsEnabled {
            get => (bool)GetValue(SplitContentPartIsEnabledProperty);
            set => SetValue(SplitContentPartIsEnabledProperty, value);
        }

        public bool StayOpen {
            get => (bool)GetValue(StayOpenProperty);
            set => SetValue(StayOpenProperty, value);
        }
    }
}
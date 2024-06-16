using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Extensions
{
    [DefaultProperty("Content")]
    [ContentProperty("Content")]
    public class BadgeControl : Control
    {
        public static readonly DependencyProperty BadgeContentProperty = DependencyProperty.Register("BadgeContent", typeof(object), typeof(BadgeControl), new PropertyMetadata(null));
        public static readonly DependencyProperty BadgeVisibilityProperty = DependencyProperty.Register("BadgeVisibility", typeof(Visibility), typeof(BadgeControl), new PropertyMetadata(Visibility.Visible));
        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register("Content", typeof(UIElement), typeof(BadgeControl), new PropertyMetadata(null));
        public static readonly DependencyProperty HorizontalBadgeAlignmentProperty = DependencyProperty.Register("HorizontalBadgeAlignment", typeof(HorizontalAlignment), typeof(BadgeControl), new PropertyMetadata(HorizontalAlignment.Right));
        public static readonly DependencyProperty VerticalBadgeAlignmentProperty = DependencyProperty.Register("VerticalBadgeAlignment", typeof(VerticalAlignment), typeof(BadgeControl), new PropertyMetadata(VerticalAlignment.Top));

        static BadgeControl() { DefaultStyleKeyProperty.OverrideMetadata(typeof(BadgeControl), new FrameworkPropertyMetadata(typeof(BadgeControl))); }

        public object BadgeContent { get => GetValue(BadgeContentProperty); set => SetValue(BadgeContentProperty, value); }

        public Visibility BadgeVisibility { get => (Visibility)GetValue(BadgeVisibilityProperty); set => SetValue(BadgeVisibilityProperty, value); }

        public UIElement Content { get => (UIElement)GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

        public HorizontalAlignment HorizontalBadgeAlignment { get => (HorizontalAlignment)GetValue(HorizontalBadgeAlignmentProperty); set => SetValue(HorizontalBadgeAlignmentProperty, value); }

        public VerticalAlignment VerticalBadgeAlignment { get => (VerticalAlignment)GetValue(VerticalBadgeAlignmentProperty); set => SetValue(VerticalBadgeAlignmentProperty, value); }
    }
}

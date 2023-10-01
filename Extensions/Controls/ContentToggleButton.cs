using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace Extensions;

public class ContentToggleButton : ToggleButton
{
    public static readonly DependencyProperty AlwaysOnTopProperty = DependencyProperty.RegisterAttached(
        "AlwaysOnTop",
        typeof(bool),
        typeof(ContentToggleButton),
        new PropertyMetadata(true, OnTopChanged));
    public static readonly DependencyProperty ArrowVisibilityProperty = DependencyProperty.Register(
        "ArrowVisibility",
        typeof(Visibility),
        typeof(ContentToggleButton),
        new PropertyMetadata(Visibility.Visible));
    public static readonly DependencyProperty ContentHorizontalOffsetProperty = DependencyProperty.Register(
        "ContentHorizontalOffset",
        typeof(double),
        typeof(ContentToggleButton),
        new PropertyMetadata(0d));
    public static readonly DependencyProperty ContentVerticalOffsetProperty = DependencyProperty.Register(
        "ContentVerticalOffset",
        typeof(double),
        typeof(ContentToggleButton),
        new PropertyMetadata(0d));
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        "CornerRadius",
        typeof(CornerRadius),
        typeof(ContentToggleButton),
        new PropertyMetadata(new CornerRadius(0d)));
    public static readonly DependencyProperty OverContentProperty = DependencyProperty.Register(
        "OverContent",
        typeof(object),
        typeof(ContentToggleButton),
        new PropertyMetadata(null));
    public static readonly DependencyProperty PlacementModeProperty = DependencyProperty.Register(
        "PlacementMode",
        typeof(PlacementMode),
        typeof(ContentToggleButton),
        new PropertyMetadata(PlacementMode.Bottom));
    public static readonly DependencyProperty PopupAnimationProperty = DependencyProperty.Register(
        "PopupAnimation",
        typeof(PopupAnimation),
        typeof(ContentToggleButton),
        new PropertyMetadata(PopupAnimation.Slide));
    public static readonly DependencyProperty StaysOpenProperty = DependencyProperty.Register(
        "StaysOpen",
        typeof(bool),
        typeof(ContentToggleButton),
        new PropertyMetadata(false));
    public static readonly DependencyProperty TopMostProperty = DependencyProperty.Register("TopMost", typeof(bool), typeof(ContentToggleButton), new PropertyMetadata(true));

    static ContentToggleButton() { DefaultStyleKeyProperty.OverrideMetadata(typeof(ContentToggleButton), new FrameworkPropertyMetadata(typeof(ContentToggleButton))); }

    public Visibility ArrowVisibility { get => (Visibility)GetValue(ArrowVisibilityProperty); set => SetValue(ArrowVisibilityProperty, value); }

    public double ContentHorizontalOffset { get => (double)GetValue(ContentHorizontalOffsetProperty); set => SetValue(ContentHorizontalOffsetProperty, value); }

    public double ContentVerticalOffset { get => (double)GetValue(ContentVerticalOffsetProperty); set => SetValue(ContentVerticalOffsetProperty, value); }

    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public object OverContent { get => GetValue(OverContentProperty); set => SetValue(OverContentProperty, value); }

    public PlacementMode PlacementMode { get => (PlacementMode)GetValue(PlacementModeProperty); set => SetValue(PlacementModeProperty, value); }

    public PopupAnimation PopupAnimation { get => (PopupAnimation)GetValue(PopupAnimationProperty); set => SetValue(PopupAnimationProperty, value); }

    public bool StaysOpen { get => (bool)GetValue(StaysOpenProperty); set => SetValue(StaysOpenProperty, value); }

    public bool TopMost { get => (bool)GetValue(TopMostProperty); set => SetValue(TopMostProperty, value); }

    public static bool GetAlwaysOnTop(DependencyObject obj) => (bool)obj.GetValue(AlwaysOnTopProperty);

    public static void SetAlwaysOnTop(DependencyObject obj, bool value) => obj.SetValue(AlwaysOnTopProperty, value);

    public override string ToString() => Content?.ToString();

    private static void OnTopChanged(DependencyObject d, DependencyPropertyChangedEventArgs f)
    {
        if (d is Popup popup)
        {
            popup.Opened += (s, e) =>
                            {
                                IntPtr hwnd = ((HwndSource)PresentationSource.FromVisual(popup.Child)).Handle;

                                if (Helpers.GetWindowRect(hwnd, out Helpers.RECT rect))
                                {
                                    _ = Helpers.SetWindowPos(hwnd, (bool)f.NewValue ? -1 : -2, rect.Left, rect.Top, (int)popup.Width, (int)popup.Height, 0);
                                }
                            };
        }
    }
}
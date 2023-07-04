using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace Extensions;

public class SplitButton : ButtonBase
{
    static SplitButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SplitButton),
            new FrameworkPropertyMetadata(typeof(SplitButton)));
    }

    public double ContentHorizontalOffset {
        get => (double)GetValue(ContentHorizontalOffsetProperty);
        set => SetValue(ContentHorizontalOffsetProperty, value);
    }

    public double ContentVerticalOffset {
        get => (double)GetValue(ContentVerticalOffsetProperty);
        set => SetValue(ContentVerticalOffsetProperty, value);
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

    public bool TopMost {
        get => (bool)GetValue(TopMostProperty);
        set => SetValue(TopMostProperty, value);
    }

    public static bool GetAlwaysOnTop(DependencyObject obj)
    {
        return (bool)obj.GetValue(AlwaysOnTopProperty);
    }

    public static void SetAlwaysOnTop(DependencyObject obj, bool value)
    {
        obj.SetValue(AlwaysOnTopProperty, value);
    }

    public static readonly DependencyProperty AlwaysOnTopProperty = DependencyProperty.RegisterAttached("AlwaysOnTop",
                                                    typeof(bool), typeof(SplitButton), new PropertyMetadata(true, OnTopChanged));

    public static readonly DependencyProperty ContentHorizontalOffsetProperty =
        DependencyProperty.Register("ContentHorizontalOffset", typeof(double), typeof(SplitButton),
            new PropertyMetadata(0d));

    public static readonly DependencyProperty ContentVerticalOffsetProperty =
        DependencyProperty.Register("ContentVerticalOffset", typeof(double), typeof(SplitButton),
            new PropertyMetadata(0d));

    public static readonly DependencyProperty InternalContentProperty =
        DependencyProperty.Register("InternalContent", typeof(object), typeof(SplitButton), new PropertyMetadata(null));

    public static readonly DependencyProperty IsSplitPartOpenProperty =
        DependencyProperty.Register("IsSplitPartOpen", typeof(bool), typeof(SplitButton), new PropertyMetadata(false));

    public static readonly DependencyProperty PlacementModeProperty = DependencyProperty.Register(
        "PlacementMode",
        typeof(PlacementMode),
        typeof(SplitButton),
        new PropertyMetadata(PlacementMode.Bottom));

    public static readonly DependencyProperty SplitContentPartIsEnabledProperty =
        DependencyProperty.Register("SplitContentPartIsEnabled", typeof(bool), typeof(SplitButton),
            new PropertyMetadata(true));

    public static readonly DependencyProperty StayOpenProperty =
        DependencyProperty.Register("StayOpen", typeof(bool), typeof(SplitButton), new PropertyMetadata(false));

    public static readonly DependencyProperty TopMostProperty =
        DependencyProperty.Register("TopMost", typeof(bool), typeof(SplitButton), new PropertyMetadata(true));

    private static void OnTopChanged(DependencyObject d, DependencyPropertyChangedEventArgs f)
    {
        if (d is Popup popup)
        {
            popup.Opened += (s, e) =>
            {
                System.IntPtr hwnd = ((HwndSource)PresentationSource.FromVisual(popup.Child)).Handle;

                if (Helpers.GetWindowRect(hwnd, out Helpers.RECT rect))
                {
                    _ = Helpers.SetWindowPos(hwnd, (bool)f.NewValue ? -1 : -2, rect.Left, rect.Top, (int)popup.Width,
                        (int)popup.Height, 0);
                }
            };
        }
    }
}
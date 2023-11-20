using System.Windows;
using System.Windows.Input;

namespace Extensions;

public static class IgnoreMouseWheelBehavior
{
    public static readonly DependencyProperty IgnoreMouseWheelProperty = DependencyProperty.RegisterAttached(
        "IgnoreMouseWheel",
        typeof(bool),
        typeof(IgnoreMouseWheelBehavior),
        new PropertyMetadata(false, OnIgnoreMouseWheelChanged));

    public static bool GetIgnoreMouseWheel(UIElement element) => (bool)element.GetValue(IgnoreMouseWheelProperty);

    public static void SetIgnoreMouseWheel(UIElement element, bool value) => element.SetValue(IgnoreMouseWheelProperty, value);

    private static void OnIgnoreMouseWheelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement uiElement)
        {
            if ((bool)e.NewValue)
            {
                uiElement.PreviewMouseWheel += UiElement_PreviewMouseWheel;
            }
            else
            {
                uiElement.PreviewMouseWheel -= UiElement_PreviewMouseWheel;
            }
        }
    }

    private static void UiElement_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        if (sender is UIElement uiElement)
        {
            MouseWheelEventArgs newEventArgs = new(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = UIElement.MouseWheelEvent, Source = uiElement };
            uiElement.RaiseEvent(newEventArgs);
        }
    }
}

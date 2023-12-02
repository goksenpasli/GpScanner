using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Markup;

namespace Extensions
{
    [DefaultProperty("TooltipContent")]
    [ContentProperty("TooltipContent")]
    public class FadedToolTipControl : Control
    {
        public static readonly DependencyProperty AlwaysOnTopProperty =
            DependencyProperty.RegisterAttached("AlwaysOnTop", typeof(bool), typeof(FadedToolTipControl), new PropertyMetadata(true, OnTopChanged));
        public static readonly DependencyProperty PopupAnimationProperty = DependencyProperty.Register("PopupAnimation", typeof(PopupAnimation), typeof(FadedToolTipControl), new PropertyMetadata(PopupAnimation.Fade));
        public static readonly DependencyProperty PopupParentProperty = DependencyProperty.Register("PopupParent", typeof(FrameworkElement), typeof(FadedToolTipControl));
        public static readonly DependencyProperty PositionProperty = DependencyProperty.Register("Position", typeof(PlacementMode), typeof(FadedToolTipControl), new PropertyMetadata(PlacementMode.Center));
        public static readonly DependencyProperty ShowCloseButtonProperty = DependencyProperty.Register("ShowCloseButton", typeof(Visibility), typeof(FadedToolTipControl), new PropertyMetadata(Visibility.Collapsed));
        public static readonly DependencyProperty ShowProperty = DependencyProperty.Register("Show", typeof(bool), typeof(FadedToolTipControl), new PropertyMetadata(false, ShowChanged));
        public static readonly DependencyProperty TimeToCloseProperty = DependencyProperty.Register("TimeToClose", typeof(int), typeof(FadedToolTipControl), new PropertyMetadata(3000));
        public static readonly DependencyProperty TimeToShowProperty = DependencyProperty.Register("TimeToShow", typeof(int), typeof(FadedToolTipControl), new PropertyMetadata(1000));
        public static readonly DependencyProperty TooltipContentProperty = DependencyProperty.Register("TooltipContent", typeof(UIElement), typeof(FadedToolTipControl));
        private Popup popup;

        static FadedToolTipControl() { DefaultStyleKeyProperty.OverrideMetadata(typeof(FadedToolTipControl), new FrameworkPropertyMetadata(typeof(FadedToolTipControl))); }

        public PopupAnimation PopupAnimation { get => (PopupAnimation)GetValue(PopupAnimationProperty); set => SetValue(PopupAnimationProperty, value); }

        public FrameworkElement PopupParent { get => (FrameworkElement)GetValue(PopupParentProperty); set => SetValue(PopupParentProperty, value); }

        public PlacementMode Position { get => (PlacementMode)GetValue(PositionProperty); set => SetValue(PositionProperty, value); }

        public bool Show { get => (bool)GetValue(ShowProperty); set => SetValue(ShowProperty, value); }

        public Visibility ShowCloseButton { get => (Visibility)GetValue(ShowCloseButtonProperty); set => SetValue(ShowCloseButtonProperty, value); }

        public int TimeToClose { get => (int)GetValue(TimeToCloseProperty); set => SetValue(TimeToCloseProperty, value); }

        public int TimeToShow { get => (int)GetValue(TimeToShowProperty); set => SetValue(TimeToShowProperty, value); }

        public UIElement TooltipContent { get => (UIElement)GetValue(TooltipContentProperty); set => SetValue(TooltipContentProperty, value); }

        public static bool GetAlwaysOnTop(DependencyObject obj) => (bool)obj.GetValue(AlwaysOnTopProperty);

        public static void SetAlwaysOnTop(DependencyObject obj, bool value) => obj.SetValue(AlwaysOnTopProperty, value);

        public override async void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            popup = GetTemplateChild("PART_Popup") as Popup;
            if (popup is not null && !DesignerProperties.GetIsInDesignMode(this))
            {
                if (Show)
                {
                    await ShowTooltipAsync();
                    return;
                }
                CloseTooltip();
            }
        }

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

        private static async void ShowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FadedToolTipControl fadedTooltipControl && fadedTooltipControl.popup is not null && !DesignerProperties.GetIsInDesignMode(fadedTooltipControl))
            {
                if ((bool)e.NewValue)
                {
                    await fadedTooltipControl.ShowTooltipAsync();
                }
                else
                {
                    fadedTooltipControl.CloseTooltip();
                }
            }
        }

        private void CloseTooltip()
        {
            if (popup is not null)
            {
                popup.IsOpen = false;
            }
        }

        private async Task ShowTooltipAsync()
        {
            if (popup is not null)
            {
                await Task.Delay(TimeToShow);
                _ = await Application.Current.Dispatcher.InvokeAsync(() => popup.IsOpen = true);

                await Task.Delay(TimeToClose);
                _ = await Application.Current.Dispatcher.InvokeAsync(() => popup.IsOpen = false);
            }
        }
    }
}

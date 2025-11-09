using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using static Extensions.NativeMethods;

namespace Extensions
{
    [DefaultProperty("Content")]
    [ContentProperty("Content")]
    public class SystemTrayIcon : Control, IDisposable
    {
        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register("Content", typeof(UIElement), typeof(SystemTrayIcon), new PropertyMetadata(null));
        public static readonly DependencyProperty DoubleClickCommandProperty = DependencyProperty.Register("DoubleClickCommand", typeof(ICommand), typeof(SystemTrayIcon));
        public static readonly DependencyProperty IconUriProperty = DependencyProperty.Register("IconUri", typeof(Uri), typeof(SystemTrayIcon));
        public static readonly DependencyProperty SingleClickCommandProperty = DependencyProperty.Register("SingleClickCommand", typeof(ICommand), typeof(SystemTrayIcon));
        public static readonly DependencyProperty ToolTipTextProperty = DependencyProperty.Register("ToolTipText", typeof(string), typeof(SystemTrayIcon));
        public static readonly DependencyProperty TrayIconActiveProperty = DependencyProperty.Register("TrayIconActive", typeof(bool), typeof(SystemTrayIcon), new PropertyMetadata(true, TrayIconActiveChanged));
        private const int WM_DESTROY = 0x0002;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_TRAYICON = WM_USER + 1;
        private const int WM_USER = 0x0400;
        private readonly Popup popup = new();
        private readonly uint WM_TASKBARCREATED = RegisterWindowMessage("TaskbarCreated");
        private NOTIFYICONDATA _notifyIconData;

        static SystemTrayIcon() { DefaultStyleKeyProperty.OverrideMetadata(typeof(SystemTrayIcon), new FrameworkPropertyMetadata(typeof(SystemTrayIcon))); }

        public SystemTrayIcon()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            popup.Closed += (s, e) => popup.Child = null;
        }

        ~SystemTrayIcon() { Dispose(); }

        public UIElement Content { get => (UIElement)GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

        public ICommand DoubleClickCommand { get => (ICommand)GetValue(DoubleClickCommandProperty); set => SetValue(DoubleClickCommandProperty, value); }

        public Uri IconUri { get => (Uri)GetValue(IconUriProperty); set => SetValue(IconUriProperty, value); }

        public ICommand SingleClickCommand { get => (ICommand)GetValue(SingleClickCommandProperty); set => SetValue(SingleClickCommandProperty, value); }

        public string ToolTipText { get => (string)GetValue(ToolTipTextProperty); set => SetValue(ToolTipTextProperty, value); }

        public bool TrayIconActive { get => (bool)GetValue(TrayIconActiveProperty); set => SetValue(TrayIconActiveProperty, value); }

        public void Dispose()
        {
            UnInitializeNotifyIcon();
            GC.SuppressFinalize(this);
        }

        public void ShowBalloonNearTray(string title, string message, int timeoutMs = 2500, int marginx = 2, int marginy = 2, SolidColorBrush backgroundColor = null, SolidColorBrush borderColor = null, SolidColorBrush textColor = null)
        {
            try
            {
                Border balloon = new()
                {
                    Background = backgroundColor ?? System.Windows.Media.Brushes.LightYellow,
                    BorderBrush = borderColor ?? System.Windows.Media.Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Child =
                    new StackPanel
                    {
                        Margin = new Thickness(8),
                        Children =
                        {
                            new TextBlock { Text = title, FontWeight = FontWeights.Bold, Foreground = textColor ?? System.Windows.Media.Brushes.Black, Margin = new Thickness(0, 0, 0, 2) },
                            new TextBlock { Text = message, Foreground = textColor ?? System.Windows.Media.Brushes.Black, TextWrapping = TextWrapping.Wrap }
                        }
                    }
                };
                balloon.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                balloon.Arrange(new Rect(balloon.DesiredSize));
                double desiredWidth = balloon.DesiredSize.Width;
                double desiredHeight = balloon.DesiredSize.Height;
                double x = SystemParameters.WorkArea.Right - desiredWidth - marginx;
                double y = SystemParameters.WorkArea.Bottom - desiredHeight - marginy;
                Popup trayPopup = new() { AllowsTransparency = true, Placement = PlacementMode.Absolute, HorizontalOffset = x, VerticalOffset = y, StaysOpen = false, Child = balloon, IsOpen = true };
                _ = Task.Delay(timeoutMs).ContinueWith(_ => Dispatcher.Invoke(() => trayPopup.IsOpen = false));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowBalloonNearTray error: {ex}");
            }
        }

        private static void TrayIconActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SystemTrayIcon systemTrayIcon)
            {
                if ((bool)e.NewValue)
                {
                    systemTrayIcon.InitializeNotifyIcon();
                }
                else
                {
                    systemTrayIcon.UnInitializeNotifyIcon();
                }
            }
        }

        private void InitializeNotifyIcon()
        {
            if (IconUri is null || !TrayIconActive)
            {
                return;
            }
            HwndSource hwndSource = PresentationSource.FromDependencyObject(this) as HwndSource;
            if (hwndSource is not null)
            {
                NOTIFYICONDATA newNOTIFYICONDATA = new() { cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)), hWnd = hwndSource.Handle, uID = 100, uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP, uCallbackMessage = WM_TRAYICON };
                using Stream streamInfo = Application.GetResourceStream(IconUri).Stream;
                using Icon icon = new(streamInfo);
                newNOTIFYICONDATA.hIcon = CopyIcon(icon.Handle);
                newNOTIFYICONDATA.szTip = ToolTipText ?? string.Empty;
                _notifyIconData = newNOTIFYICONDATA;
                _ = Shell_NotifyIcon(NIM_ADD, ref _notifyIconData);
                hwndSource.AddHook(WndProc);
            }
            else
            {
                hwndSource?.Dispose();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                return;
            }
            InitializeNotifyIcon();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) => UnInitializeNotifyIcon();

        private void ShowContextMenu()
        {
            if (ContextMenu is null)
            {
                return;
            }
            ContextMenu.PlacementTarget = this;
            ContextMenu.Placement = PlacementMode.MousePoint;
            ContextMenu.IsOpen = true;
        }

        private void ShowPopup()
        {
            if (popup is null || Content is null)
            {
                return;
            }
            popup.Child = Content;
            popup.AllowsTransparency = true;
            popup.IsOpen = true;
            popup.PlacementTarget = this;
            popup.Placement = PlacementMode.MousePoint;
        }

        private void UnInitializeNotifyIcon() => Shell_NotifyIcon(NIM_DELETE, ref _notifyIconData);

        [DebuggerStepThrough]
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_TASKBARCREATED && TrayIconActive)
            {
                InitializeNotifyIcon();
            }
            switch (msg)
            {
                case WM_DESTROY:
                    _ = Shell_NotifyIcon(NIM_DELETE, ref _notifyIconData);
                    break;
                case WM_LBUTTONDOWN:
                    if (Content is not null && popup is not null)
                    {
                        popup.IsOpen = false;
                    }
                    break;
                case WM_TRAYICON:
                    if (lParam.ToInt32() is WM_LBUTTONDOWN)
                    {
                        if (popup is not null)
                        {
                            if (!popup.IsOpen)
                            {
                                ShowPopup();
                            }
                            else
                            {
                                popup.IsOpen = false;
                            }
                        }
                        SingleClickCommand?.Execute(null);
                        handled = true;
                        break;
                    }
                    if (lParam.ToInt32() is WM_LBUTTONDBLCLK)
                    {
                        DoubleClickCommand?.Execute(null);
                        handled = true;
                        break;
                    }
                    if (lParam.ToInt32() is WM_RBUTTONDOWN)
                    {
                        ShowContextMenu();
                        handled = true;
                        break;
                    }
                    break;
            }
            return IntPtr.Zero;
        }
    }

    internal static class NativeMethods
    {
        internal const int IMAGE_ICON = 1;
        internal const int LR_LOADFROMFILE = 0x10;
        internal const int NIF_ICON = 0x00000002;
        internal const int NIF_MESSAGE = 0x00000001;
        internal const int NIF_TIP = 0x00000004;
        internal const int NIM_ADD = 0x00000000;
        internal const int NIM_DELETE = 0x00000002;
        internal const int WM_MOUSEMOVE = 0x0200;
        internal const int WM_TASKBARCREATED = 0x8000;

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr CopyIcon(IntPtr hIcon);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern uint RegisterWindowMessage(string msgString);

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr Shell_NotifyIcon(int nMessage, ref NOTIFYICONDATA pnid);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct NOTIFYICONDATA
        {
            internal int cbSize;
            internal IntPtr hWnd;
            internal int uID;
            internal int uFlags;
            internal int uCallbackMessage;
            internal IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            internal string szTip;
        }
    }
}

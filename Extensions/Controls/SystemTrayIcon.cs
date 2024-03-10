using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Resources;
using static Extensions.NativeMethods;

namespace Extensions
{
    public class SystemTrayIcon : Control
    {
        public static readonly DependencyProperty DoubleClickCommandProperty = DependencyProperty.Register("DoubleClickCommand", typeof(ICommand), typeof(SystemTrayIcon));
        public static readonly DependencyProperty IconUriProperty = DependencyProperty.Register("IconUri", typeof(Uri), typeof(SystemTrayIcon));
        public static readonly DependencyProperty SingleClickCommandProperty = DependencyProperty.Register("SingleClickCommand", typeof(ICommand), typeof(SystemTrayIcon));
        public static readonly DependencyProperty ToolTipTextProperty = DependencyProperty.Register("ToolTipText", typeof(string), typeof(SystemTrayIcon));
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_TASKBARCREATED = 0x8000;
        private const int WM_TRAYICON = WM_USER + 1;
        private const int WM_USER = 0x0400;
        private NOTIFYICONDATA _notifyIconData;

        static SystemTrayIcon() { DefaultStyleKeyProperty.OverrideMetadata(typeof(SystemTrayIcon), new FrameworkPropertyMetadata(typeof(SystemTrayIcon))); }

        public SystemTrayIcon()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public ICommand DoubleClickCommand { get => (ICommand)GetValue(DoubleClickCommandProperty); set => SetValue(DoubleClickCommandProperty, value); }

        public Uri IconUri { get => (Uri)GetValue(IconUriProperty); set => SetValue(IconUriProperty, value); }

        public ICommand SingleClickCommand { get => (ICommand)GetValue(SingleClickCommandProperty); set => SetValue(SingleClickCommandProperty, value); }

        public string ToolTipText { get => (string)GetValue(ToolTipTextProperty); set => SetValue(ToolTipTextProperty, value); }

        private void InitializeNotifyIcon()
        {
            if (IconUri == null)
            {
                return;
            }
            HwndSource hwndSource = PresentationSource.FromDependencyObject(this) as HwndSource;
            NOTIFYICONDATA newNOTIFYICONDATA = new() { hWnd = hwndSource.Handle, uID = 100, uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP, uCallbackMessage = WM_TRAYICON };
            StreamResourceInfo streamInfo = Application.GetResourceStream(IconUri);
            using Icon icon = new(streamInfo.Stream, new System.Drawing.Size(16, 16));
            newNOTIFYICONDATA.hIcon = icon.Handle;
            newNOTIFYICONDATA.szTip = ToolTipText ?? string.Empty;
            _notifyIconData = newNOTIFYICONDATA;
            _ = Shell_NotifyIcon(NIM_ADD, ref _notifyIconData);
            streamInfo.Stream.Dispose();
            hwndSource.AddHook(WndProc);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                return;
            }
            InitializeNotifyIcon();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) => Shell_NotifyIcon(NIM_DELETE, ref _notifyIconData);

        private void ShowContextMenu()
        {
            if (ContextMenu != null)
            {
                ContextMenu.PlacementTarget = this;
                ContextMenu.Placement = PlacementMode.MousePoint;
                ContextMenu.IsOpen = true;
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_TASKBARCREATED:
                    InitializeNotifyIcon();
                    break;
                case WM_TRAYICON:
                    if (lParam.ToInt32() is WM_LBUTTONDOWN)
                    {
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

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr LoadImage(int Hinstance, string name, int type, int width, int height, int load);

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

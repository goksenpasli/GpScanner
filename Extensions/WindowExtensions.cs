using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Extensions
{
    public static class WindowExtensions
    {
        public const int GWL_STYLE = -16, WS_MINIMIZEBOX = 0x20000;
        public const uint MF_BYCOMMAND = 0x00000000;
        public const int MF_BYPOSITION = 0x400;
        public const uint MF_ENABLED = 0x00000000;
        public const uint MF_GRAYED = 0x00000001;
        public const uint SC_CLOSE = 0xF060;
        public const int VK_F4 = 0x73;
        public const int WM_SYSCOMMAND = 0x112;
        public const int WM_SYSKEYDOWN = 0x0104;

        public static void DisableCloseButton(this Window window, bool disable)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            IntPtr sysMenu = GetSystemMenu(hwnd, false);
            _ = disable ? EnableMenuItem(sysMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED) : EnableMenuItem(sysMenu, SC_CLOSE, MF_BYCOMMAND | MF_ENABLED);
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetSystemMenu(this IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool InsertMenu(this IntPtr hMenu, uint wPosition, uint wFlags, int wIDNewItem, string lpNewItem);

        internal static void HideMinimizeButtons(this Window window)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            int currentStyle = GetWindowLong(hwnd, GWL_STYLE);

            _ = SetWindowLong(hwnd, GWL_STYLE, currentStyle & ~WS_MINIMIZEBOX);
        }

        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    }
}

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using TwainControl;

namespace GpScanner.ViewModel;

public static class WindowExtensions
{
    private const int _AboutSysMenuID = 1001;
    private const int GWL_STYLE = -16, WS_MINIMIZEBOX = 0x20000;
    private const uint MF_BYCOMMAND = 0x00000000;
    private const int MF_BYPOSITION = 0x400;
    private const uint MF_ENABLED = 0x00000000;
    private const uint MF_GRAYED = 0x00000001;
    private const uint SC_CLOSE = 0xF060;
    private const int WM_SYSCOMMAND = 0x112;

    public static void DisableCloseButton(this Window window, bool disable)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        IntPtr sysMenu = GetSystemMenu(hwnd, false);
        _ = disable ? EnableMenuItem(sysMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED) : EnableMenuItem(sysMenu, SC_CLOSE, MF_BYCOMMAND | MF_ENABLED);
    }

    public static void SystemMenu(this MainWindow form)
    {
        IntPtr systemMenuHandle = GetSystemMenu(new WindowInteropHelper(form).Handle, false);
        _ = InsertMenu(systemMenuHandle, 7, MF_BYPOSITION, _AboutSysMenuID, Translation.GetResStringValue("ABOUT"));
        HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(form).Handle);
        source.AddHook(WndProc);
    }

    internal static void HideMinimizeButtons(this Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        int currentStyle = GetWindowLong(hwnd, GWL_STYLE);

        _ = SetWindowLong(hwnd, GWL_STYLE, currentStyle & ~WS_MINIMIZEBOX);
    }

    [DllImport("user32.dll")]
    private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

    [DllImport("user32.dll")]
    private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool InsertMenu(IntPtr hMenu, uint wPosition, uint wFlags, int wIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [DebuggerStepThrough]
    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_SYSCOMMAND)
        {
            switch (wParam.ToInt32())
            {
                case _AboutSysMenuID:
                    _ = Process.Start("https://github.com/goksenpasli");
                    handled = true;
                    break;
            }
        }

        return IntPtr.Zero;
    }
}
using System;

namespace TwainWpf {
    public delegate IntPtr FilterMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled);

    public interface IWindowsMessageHook {
        /// <summary>
        /// The delegate to call back then the filter is in place and a message arrives.
        /// </summary>
        FilterMessage FilterMessageCallback { get; set; }

        /// <summary>
        /// Gets or sets if the message filter is in use.
        /// </summary>
        bool UseFilter { get; set; }

        /// <summary>
        /// The handle to the window that is performing the scanning.
        /// </summary>
        IntPtr WindowHandle { get; }
    }
}
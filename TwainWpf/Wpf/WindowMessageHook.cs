using System;
using System.Windows;
using System.Windows.Interop;

namespace TwainWpf.Wpf
{
    public class WindowMessageHook : IWindowsMessageHook
    {
        public WindowMessageHook(Window window)
        {
            _source = (HwndSource)PresentationSource.FromDependencyObject(window);
            _interopHelper = new WindowInteropHelper(window);
        }

        public FilterMessage FilterMessageCallback { get; set; }

        public bool UseFilter
        {
            get => _usingFilter;

            set
            {
                if(!_usingFilter && value)
                {
                    _source.AddHook(FilterMessage);
                    _usingFilter = true;
                }
                if(_usingFilter && !value)
                {
                    _source.RemoveHook(FilterMessage);
                    _usingFilter = false;
                }
            }
        }

        public IntPtr WindowHandle => _interopHelper.Handle;

        public IntPtr FilterMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        { return FilterMessageCallback == null ? IntPtr.Zero : FilterMessageCallback(hwnd, msg, wParam, lParam, ref handled); }

        private readonly WindowInteropHelper _interopHelper;

        private readonly HwndSource _source;

        private bool _usingFilter;
    }
}
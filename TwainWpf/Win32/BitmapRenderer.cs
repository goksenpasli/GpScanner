using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace TwainWpf.Win32
{
    public class BitmapRenderer : IDisposable
    {
        public BitmapRenderer(IntPtr dibHandle)
        {
            _dibHandle = dibHandle;
            _bitmapPointer = Kernel32Native.GlobalLock(dibHandle);

            _bitmapInfo = new BitmapInfoHeader();
            Marshal.PtrToStructure(_bitmapPointer, _bitmapInfo);

            _rectangle = new Rectangle();
            _rectangle.X = _rectangle.Y = 0;
            _rectangle.Width = _bitmapInfo.Width;
            _rectangle.Height = _bitmapInfo.Height;

            if (_bitmapInfo.SizeImage == 0)
            {
                _bitmapInfo.SizeImage = ((((_bitmapInfo.Width * _bitmapInfo.BitCount) + 31) & ~31) >> 3) * _bitmapInfo.Height;
            }

            Debug.Assert(Marshal.SizeOf(typeof(IntPtr)) == 4);

            int pixelInfoPointer = _bitmapInfo.ClrUsed;
            if (pixelInfoPointer == 0 && _bitmapInfo.BitCount <= 8)
            {
                pixelInfoPointer = 1 << (_bitmapInfo.BitCount);
            }
            pixelInfoPointer = (pixelInfoPointer * 4) + _bitmapInfo.Size + _bitmapPointer.ToInt32();

            _pixelInfoPointer = new IntPtr(pixelInfoPointer);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public Bitmap RenderToBitmap()
        {
            Bitmap bitmap = new Bitmap(_rectangle.Width, _rectangle.Height);

            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                IntPtr hdc = graphics.GetHdc();

                try
                {
                    _ = Gdi32Native.SetDIBitsToDevice(hdc, 0, 0, _rectangle.Width, _rectangle.Height, 0, 0, 0, _rectangle.Height, _pixelInfoPointer, _bitmapPointer, 0);
                }
                finally
                {
                    graphics.ReleaseHdc(hdc);
                }
            }

            bitmap.SetResolution(PpmToDpi(_bitmapInfo.XPelsPerMeter), PpmToDpi(_bitmapInfo.YPelsPerMeter));

            return bitmap;
        }

        protected virtual void Dispose(bool disposing)
        {
            _ = Kernel32Native.GlobalUnlock(_dibHandle);
            _ = Kernel32Native.GlobalFree(_dibHandle);
        }

        private readonly BitmapInfoHeader _bitmapInfo;

        private readonly IntPtr _bitmapPointer;

        private readonly IntPtr _dibHandle;

        private readonly IntPtr _pixelInfoPointer;

        private Rectangle _rectangle;

        ~BitmapRenderer()
        {
            Dispose(false);
        }

        private static float PpmToDpi(double pixelsPerMeter)
        {
            double pixelsPerMillimeter = pixelsPerMeter / 1000.0;
            double dotsPerInch = pixelsPerMillimeter * 25.4;
            return (float)Math.Round(dotsPerInch, 2);
        }
    }
}
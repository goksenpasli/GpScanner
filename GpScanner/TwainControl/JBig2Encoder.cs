using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TwainControl
{
    public static class JBig2Encoder
    {
        public static byte[] Encode(Bitmap bmp, bool zeroIsWhite)
        {
            BitmapData bits = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format1bppIndexed);
            byte[] bytes = Encode(bmp.Width, bmp.Height, bits.Stride, zeroIsWhite, bits.Scan0);
            bmp.UnlockBits(bits);
            return bytes;
        }

        private static byte[] Encode(int width, int height, int stride, bool zeroIsWhite, IntPtr b)
        {
            int l = 0;
            IntPtr r = NativeMethods.jbig2_encode(width, height, stride, zeroIsWhite, b, ref l);
            byte[] result = new byte[l];
            Marshal.Copy(r, result, 0, l);
            _ = NativeMethods.release(r);
            return result;
        }

        private static class NativeMethods
        {
            [DllImport("jbig2enc.dll")]
            internal static extern IntPtr jbig2_encode(int width, int height, int stride, bool zeroIsWhite, IntPtr data, ref int length);

            [DllImport("jbig2enc.dll")]
            internal static extern IntPtr release(IntPtr data);
        }
    }
}

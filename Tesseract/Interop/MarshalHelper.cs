using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Tesseract.Interop
{
    internal static unsafe class MarshalHelper
    {
        public static string PtrToString(IntPtr handle, Encoding encoding)
        {
            int length = StrLength(handle);
            return new string((sbyte*)handle.ToPointer(), 0, length, encoding);
        }

        public static IntPtr StringToPtr(string value, Encoding encoding)
        {
            _ = encoding.GetEncoder();
            int length = encoding.GetByteCount(value);

            byte[] encodedValue = new byte[length + 1];
            _ = encoding.GetBytes(value, 0, value.Length, encodedValue, 0);
            IntPtr handle = Marshal.AllocHGlobal(new IntPtr(encodedValue.Length));
            Marshal.Copy(encodedValue, 0, handle, encodedValue.Length);
            return handle;
        }

        /// <summary>
        ///     Gets the number of bytes in a null terminated byte array.
        /// </summary>
        public static int StrLength(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return 0;
            }

            byte* ptr = (byte*)handle.ToPointer();
            int length = 0;
            while (*(ptr + length) != 0)
            {
                length++;
            }

            return length;
        }
    }
}
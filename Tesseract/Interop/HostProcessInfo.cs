﻿using System;

namespace Tesseract.Interop
{
    /// <summary>
    /// Provides information about the hosting process.
    /// </summary>
    internal static class HostProcessInfo
    {
        static HostProcessInfo()
        {
            Is64Bit = IntPtr.Size == 8;
        }

        public static readonly bool Is64Bit;
    }
}
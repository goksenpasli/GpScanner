﻿using System;

namespace Tesseract.Internal.InteropDotNet
{
    internal static class SystemManager
    {
        public static string GetPlatformName() { return IntPtr.Size == sizeof(int) ? "x86" : "x64"; }

        public static OperatingSystem GetOperatingSystem()
        {
#if NETCORE || NETSTANDARD
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return OperatingSystem.Windows;
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return OperatingSystem.Unix;
            if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return OperatingSystem.MacOSX;

            return OperatingSystem.Unknown;
#else
            switch((int)Environment.OSVersion.Platform)
            {
                case (int)PlatformID.Win32NT:
                case (int)PlatformID.Win32S:
                case (int)PlatformID.Win32Windows:
                case (int)PlatformID.WinCE:
                    return OperatingSystem.Windows;

                case (int)PlatformID.Unix:
                case 128:
                    return OperatingSystem.Unix;

                case (int)PlatformID.MacOSX:
                    return OperatingSystem.MacOSX;

                default:
                    return OperatingSystem.Unknown;
            }
#endif
        }
    }

    internal enum OperatingSystem
    {
        Windows,

        Unix,

        MacOSX,

        Unknown
    }
}
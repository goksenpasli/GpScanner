﻿//  Copyright (c) 2014 Andrey Akinshin
//  Project URL: https://github.com/AndreyAkinshin/InteropDotNet
//  Distributed under the MIT License: http://opensource.org/licenses/MIT
using System;

namespace Tesseract.Internal.InteropDotNet
{
    internal static class SystemManager
    {
        public static string GetPlatformName()
        {
            return IntPtr.Size == sizeof(int) ? "x86" : "x64";
        }

        public static OperatingSystem GetOperatingSystem()
        {
            // Environment.OSVersion.Platform detects MacOS as Unix in .net core environment
#if NETCORE || NETSTANDARD
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return OperatingSystem.Windows;
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return OperatingSystem.Unix;
            if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return OperatingSystem.MacOSX;

            return OperatingSystem.Unknown;
#else
            int pid = (int)Environment.OSVersion.Platform;
            switch (pid)
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
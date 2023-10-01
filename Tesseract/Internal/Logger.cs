﻿using System.Diagnostics;
using System.Globalization;

namespace Tesseract.Internal
{
    internal static class Logger
    {
        private static readonly TraceSource trace = new TraceSource("Tesseract");

        public static void TraceError(string format, params object[] args) => trace.TraceEvent(
            TraceEventType.Error,
            0,
            string.Format(CultureInfo.CurrentCulture, format, args));

        public static void TraceInformation(string format, params object[] args) => trace.TraceEvent(
            TraceEventType.Information,
            0,
            string.Format(CultureInfo.CurrentCulture, format, args));

        public static void TraceWarning(string format, params object[] args) => trace.TraceEvent(
            TraceEventType.Warning,
            0,
            string.Format(CultureInfo.CurrentCulture, format, args));
    }
}
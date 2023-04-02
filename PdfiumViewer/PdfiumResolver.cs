using System;

namespace PdfiumViewer
{
    public static class PdfiumResolver
    {
        public static event EventHandler<PdfiumResolveEventArgs> Resolve;

        internal static string GetPdfiumFileName()
        {
            PdfiumResolveEventArgs e = new PdfiumResolveEventArgs();
            OnResolve(e);
            return e.PdfiumFileName;
        }

        private static void OnResolve(PdfiumResolveEventArgs e)
        {
            Resolve?.Invoke(null, e);
        }
    }
}
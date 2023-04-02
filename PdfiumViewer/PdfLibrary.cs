using System;

namespace PdfiumViewer
{
    internal class PdfLibrary : IDisposable
    {
        public static void EnsureLoaded()
        {
            lock (_syncRoot)
            {
                if (_library == null)
                {
                    _library = new PdfLibrary();
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        private static readonly object _syncRoot = new object();

        private static PdfLibrary _library;

        private bool _disposed;

        private PdfLibrary()
        {
            NativeMethods.FPDF_AddRef();
        }

        ~PdfLibrary()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                NativeMethods.FPDF_Release();

                _disposed = true;
            }
        }
    }
}
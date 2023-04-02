using System;

namespace PdfiumViewer
{
    public delegate void PdfiumResolveEventHandler(object sender, PdfiumResolveEventArgs e);

    public class PdfiumResolveEventArgs : EventArgs
    {
        public string PdfiumFileName { get; set; }
    }
}
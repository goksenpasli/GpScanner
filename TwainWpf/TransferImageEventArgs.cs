using System;
using System.Drawing;

namespace TwainWpf
{
    public class TransferImageEventArgs : EventArgs
    {
        public TransferImageEventArgs(Bitmap image, bool continueScanning)
        {
            Image = image;
            ContinueScanning = continueScanning;
        }

        public bool ContinueScanning { get; set; }

        public Bitmap Image { get; private set; }
    }
}
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PdfiumViewer
{
    public delegate void SetCursorEventHandler(object sender, SetCursorEventArgs e);

    public class SetCursorEventArgs : EventArgs
    {
        public SetCursorEventArgs(Point location, HitTest hitTest)
        {
            Location = location;
            HitTest = hitTest;
        }

        public Cursor Cursor { get; set; }

        public HitTest HitTest { get; private set; }

        public Point Location { get; private set; }
    }
}
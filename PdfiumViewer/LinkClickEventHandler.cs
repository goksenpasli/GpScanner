using System.ComponentModel;

namespace PdfiumViewer
{
    public delegate void LinkClickEventHandler(object sender, LinkClickEventArgs e);

    public class LinkClickEventArgs : HandledEventArgs
    {
        public LinkClickEventArgs(PdfPageLink link)
        {
            Link = link;
        }

        /// <summary>
        /// Gets the link that was clicked.
        /// </summary>
        public PdfPageLink Link { get; private set; }
    }
}
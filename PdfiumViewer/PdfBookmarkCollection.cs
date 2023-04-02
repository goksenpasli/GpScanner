using System.Collections.ObjectModel;

namespace PdfiumViewer
{
    public class PdfBookmark
    {
        public PdfBookmark()
        {
            Children = new PdfBookmarkCollection();
        }

        public PdfBookmarkCollection Children { get; }

        public int PageIndex { get; set; }

        public string Title { get; set; }

        public override string ToString()
        {
            return Title;
        }
    }

    public class PdfBookmarkCollection : Collection<PdfBookmark>
    {
    }
}
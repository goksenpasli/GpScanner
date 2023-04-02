using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PdfiumViewer
{
    public class PdfMatches
    {
        public PdfMatches(int startPage, int endPage, IList<PdfMatch> matches)
        {
            if (matches == null)
            {
                throw new ArgumentNullException(nameof(matches));
            }

            StartPage = startPage;
            EndPage = endPage;
            Items = new ReadOnlyCollection<PdfMatch>(matches);
        }

        public int EndPage { get; private set; }

        public IList<PdfMatch> Items { get; private set; }

        public int StartPage { get; private set; }
    }
}
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PdfiumViewer
{
    /// <summary>
    /// Describes all links on a page.
    /// </summary>
    public class PdfPageLinks
    {
        /// <summary>
        /// Creates a new instance of the PdfPageLinks class.
        /// </summary>
        /// <param name="links">The links on the PDF page.</param>
        public PdfPageLinks(IList<PdfPageLink> links)
        {
            if (links == null)
            {
                throw new ArgumentNullException(nameof(links));
            }

            Links = new ReadOnlyCollection<PdfPageLink>(links);
        }

        /// <summary>
        /// All links of the page.
        /// </summary>
        public IList<PdfPageLink> Links { get; private set; }
    }
}
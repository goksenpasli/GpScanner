using System;
using System.Drawing;
using System.Drawing.Printing;

namespace PdfiumViewer
{
    internal class PdfPrintDocument : PrintDocument
    {
        public PdfPrintDocument(IPdfDocument document, PdfPrintSettings settings)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _settings = settings;
        }

        public event EventHandler<PrintPageEventArgs> BeforePrintPage;

        public event EventHandler<QueryPageSettingsEventArgs> BeforeQueryPageSettings;

        protected virtual void OnBeforePrintPage(PrintPageEventArgs e)
        {
            BeforePrintPage?.Invoke(this, e);
        }

        protected virtual void OnBeforeQueryPageSettings(QueryPageSettingsEventArgs e)
        {
            BeforeQueryPageSettings?.Invoke(this, e);
        }

        protected override void OnBeginPrint(PrintEventArgs e)
        {
            _currentPage = PrinterSettings.FromPage == 0 ? 0 : PrinterSettings.FromPage - 1;

            base.OnBeginPrint(e);
        }

        protected override void OnPrintPage(PrintPageEventArgs e)
        {
            OnBeforePrintPage(e);

            if (_settings.MultiplePages != null)
            {
                PrintMultiplePages(e);
            }
            else
            {
                PrintSinglePage(e);
            }

            base.OnPrintPage(e);
        }

        protected override void OnQueryPageSettings(QueryPageSettingsEventArgs e)
        {
            OnBeforeQueryPageSettings(e);

            // Some printers misreport landscape. The below check verifies
            // whether the page rotation matches the landscape setting.
            bool inverseLandscape = (e.PageSettings.Bounds.Width > e.PageSettings.Bounds.Height) != e.PageSettings.Landscape;

            if (_settings.MultiplePages == null && _currentPage < _document.PageCount)
            {
                bool landscape = GetOrientation(_document.PageSizes[_currentPage]) == Orientation.Landscape;

                if (inverseLandscape)
                {
                    landscape = !landscape;
                }

                e.PageSettings.Landscape = landscape;
            }

            base.OnQueryPageSettings(e);
        }

        private readonly IPdfDocument _document;

        private readonly PdfPrintSettings _settings;

        private int _currentPage;

        private enum Orientation
        {
            Portrait,

            Landscape
        }

        private static int AdjustDpi(double value, double dpi)
        {
            return (int)(value / 100.0 * dpi);
        }

        private static void Swap(ref double a, ref double b)
        {
            var tmp = a;
            a = b;
            b = tmp;
        }

        private Orientation GetOrientation(SizeF pageSize)
        {
            return pageSize.Height > pageSize.Width ? Orientation.Portrait : Orientation.Landscape;
        }

        private void PrintMultiplePages(PrintPageEventArgs e)
        {
            PdfPrintMultiplePages settings = _settings.MultiplePages;

            int pagesPerPage = settings.Horizontal * settings.Vertical;
            int pageCount = ((_document.PageCount - 1) / pagesPerPage) + 1;

            if (_currentPage < pageCount)
            {
                double width = e.PageBounds.Width - (e.PageSettings.HardMarginX * 2);
                double height = e.PageBounds.Height - (e.PageSettings.HardMarginY * 2);

                double widthPerPage = (width - ((settings.Horizontal - 1) * settings.Margin)) / settings.Horizontal;
                double heightPerPage = (height - ((settings.Vertical - 1) * settings.Margin)) / settings.Vertical;

                for (int horizontal = 0; horizontal < settings.Horizontal; horizontal++)
                {
                    for (int vertical = 0; vertical < settings.Vertical; vertical++)
                    {
                        int page = _currentPage * pagesPerPage;
                        if (settings.Orientation == System.Windows.Forms.Orientation.Horizontal)
                        {
                            page += (vertical * settings.Vertical) + horizontal;
                        }
                        else
                        {
                            page += (horizontal * settings.Horizontal) + vertical;
                        }

                        if (page >= _document.PageCount)
                        {
                            continue;
                        }

                        double pageLeft = (widthPerPage + settings.Margin) * horizontal;
                        double pageTop = (heightPerPage + settings.Margin) * vertical;

                        RenderPage(e, page, pageLeft, pageTop, widthPerPage, heightPerPage);
                    }
                }

                _currentPage++;
            }

            if (PrinterSettings.ToPage > 0)
            {
                pageCount = Math.Min(PrinterSettings.ToPage, pageCount);
            }

            e.HasMorePages = _currentPage < pageCount;
        }

        private void PrintSinglePage(PrintPageEventArgs e)
        {
            if (_currentPage < _document.PageCount)
            {
                Orientation pageOrientation = GetOrientation(_document.PageSizes[_currentPage]);
                Orientation printOrientation = GetOrientation(e.PageBounds.Size);

                e.PageSettings.Landscape = pageOrientation == Orientation.Landscape;

                double left;
                double top;
                double width;
                double height;

                if (_settings.Mode == PdfPrintMode.ShrinkToMargin)
                {
                    left = 0;
                    top = 0;
                    width = e.PageBounds.Width - (e.PageSettings.HardMarginX * 2);
                    height = e.PageBounds.Height - (e.PageSettings.HardMarginY * 2);
                }
                else
                {
                    left = -e.PageSettings.HardMarginX;
                    top = -e.PageSettings.HardMarginY;
                    width = e.PageBounds.Width;
                    height = e.PageBounds.Height;
                }

                if (pageOrientation != printOrientation)
                {
                    Swap(ref height, ref width);
                    Swap(ref left, ref top);
                }

                RenderPage(e, _currentPage, left, top, width, height);
                _currentPage++;
            }

            int pageCount = PrinterSettings.ToPage == 0
                ? _document.PageCount
                : Math.Min(PrinterSettings.ToPage, _document.PageCount);

            e.HasMorePages = _currentPage < pageCount;
        }

        private void RenderPage(PrintPageEventArgs e, int page, double left, double top, double width, double height)
        {
            SizeF size = _document.PageSizes[page];

            double pageScale = size.Height / size.Width;
            double printScale = height / width;

            double scaledWidth = width;
            double scaledHeight = height;

            if (pageScale > printScale)
            {
                scaledWidth = width * (printScale / pageScale);
            }
            else
            {
                scaledHeight = height * (pageScale / printScale);
            }

            left += (width - scaledWidth) / 2;
            top += (height - scaledHeight) / 2;

            _document.Render(
                page,
                e.Graphics,
                e.Graphics.DpiX,
                e.Graphics.DpiY,
                new Rectangle(
                    AdjustDpi(e.Graphics.DpiX, left),
                    AdjustDpi(e.Graphics.DpiY, top),
                    AdjustDpi(e.Graphics.DpiX, scaledWidth),
                    AdjustDpi(e.Graphics.DpiY, scaledHeight)
                ),
                PdfRenderFlags.ForPrinting | PdfRenderFlags.Annotations
            );
        }
    }
}
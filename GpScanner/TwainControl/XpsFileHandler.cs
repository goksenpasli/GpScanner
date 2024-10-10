using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Xps.Packaging;

namespace TwainControl
{
    public class XpsFileHandler : ILoadFileHandler
    {
        public Task<MemoryStream> ConvertToImageStreamAsync(byte[] fileData, int pageNumber) => throw new NotImplementedException();

        public int GetPageCount(string filename)
        {
            FixedDocumentSequence docSeq = null;
            Application.Current?.Dispatcher?
            .Invoke(
                () =>
                {
                    using XpsDocument xpsDoc = new(filename, FileAccess.Read);
                    docSeq = xpsDoc.GetFixedDocumentSequence();
                });
            return docSeq.DocumentPaginator.PageCount;
        }

        public bool IsValidFile(string filename) => Path.GetExtension(filename.ToLowerInvariant()) == ".xps";

        public Task<BitmapFrame> LoadImageAsync(string filename) => throw new NotImplementedException();

        public Task<IEnumerable<BitmapFrame>> LoadTiffPagesAsync(string filename) => throw new NotImplementedException();

        public BitmapFrame LoadWebpImage(int decodeheight, string filename) => throw new NotImplementedException();

        public async Task<IEnumerable<BitmapFrame>> LoadXpsPagesAsync(string filename)
        {
            if (!IsValidFile(filename))
            {
                return null;
            }
            List<BitmapFrame> frames = [];
            await Application.Current?.Dispatcher?
            .InvokeAsync(
                () =>
                {
                    using XpsDocument xpsDoc = new(filename, FileAccess.Read);
                    FixedDocumentSequence docSeq = xpsDoc.GetFixedDocumentSequence();
                    int pageCount = docSeq.DocumentPaginator.PageCount;

                    for (int i = 0; i < pageCount; i++)
                    {
                        using DocumentPage docPage = docSeq.DocumentPaginator.GetPage(i);
                        RenderTargetBitmap rtb = new((int)docPage.Size.Width, (int)docPage.Size.Height, 96, 96, PixelFormats.Default);
                        rtb.Render(docPage.Visual);
                        BitmapFrame bitmapFrame = BitmapFrame.Create(rtb);
                        bitmapFrame.Freeze();
                        frames.Add(bitmapFrame);
                    }
                });
            return frames;
        }

        public async Task<BitmapFrame> LoadXpsSinglePagesAsync(string filename, int pagenumber = 0)
        {
            return !IsValidFile(filename)
                ? null
                : await Application.Current?.Dispatcher?
            .InvokeAsync(
                () =>
                {
                    using XpsDocument xpsDoc = new(filename, FileAccess.Read);
                    FixedDocumentSequence docSeq = xpsDoc.GetFixedDocumentSequence();
                    using DocumentPage docPage = docSeq.DocumentPaginator.GetPage(pagenumber);
                    RenderTargetBitmap rtb = new((int)docPage.Size.Width, (int)docPage.Size.Height, 96, 96, PixelFormats.Default);
                    rtb.Render(docPage.Visual);
                    BitmapFrame bitmapFrame = BitmapFrame.Create(rtb);
                    bitmapFrame.Freeze();
                    return bitmapFrame;
                });
        }
    }
}

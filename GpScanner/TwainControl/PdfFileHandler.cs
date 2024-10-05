using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using TwainControl.Properties;
using Viewer = PdfViewer.PdfViewer;

namespace TwainControl
{
    public class PdfFileHandler : ILoadFileHandler
    {
        public async Task<MemoryStream> ConvertToImageStreamAsync(byte[] fileData, int pageNumber) => await Viewer.ConvertToImgStreamAsync(fileData, pageNumber, Settings.Default.ImgLoadResolution);

        public int GetPageCount(string filename) => Viewer.PdfPageCount(filename);

        public bool IsValidFile(string filename) => Viewer.IsValidPdfFile(filename);

        public Task<BitmapFrame> LoadImageAsync(string filename) => throw new NotImplementedException();

        public async Task<BitmapImage> LoadImageAsync(string filename, int pageNumber)
        {
            if (!IsValidFile(filename))
            {
                return null;
            }
            return await Viewer.ConvertToImgAsync(filename, pageNumber, Settings.Default.ImgLoadResolution);
        }

        public Task<IEnumerable<BitmapFrame>> LoadTiffPagesAsync(string filename) => throw new NotImplementedException();

        public BitmapFrame LoadWebpImage(int decodeheight, string filename) => throw new NotImplementedException();

        public Task<IEnumerable<BitmapFrame>> LoadXpsPagesAsync(string filename) => throw new NotImplementedException();
    }
}

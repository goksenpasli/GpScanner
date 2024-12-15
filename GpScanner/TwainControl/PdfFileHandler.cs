using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using TwainControl.Properties;
using Viewer = PdfViewer.PdfViewer;

namespace TwainControl
{
    public class PdfFileHandler : ILoadFileHandler
    {
        public int GetPageCount(string filename) => Viewer.PdfPageCount(filename);

        public IEnumerable<PdfCharacterInformation> GetPdfCharacters() => Viewer.CharacterInformations;

        public bool IsValidFile(string filename) => Viewer.IsValidPdfFile(filename);

        public Task<BitmapFrame> LoadImageAsync(string filename) => throw new NotImplementedException();

        public async Task<BitmapImage> LoadPdfAsync(string filename, int pageNumber) => await Viewer.ConvertToImgAsync(filename, pageNumber, Settings.Default.ImgLoadResolution);

        public Task<IEnumerable<BitmapFrame>> LoadTiffPagesAsync(string filename) => throw new NotImplementedException();

        public Task<BitmapFrame> LoadWebpImage(int decodeheight, string filename, bool fullresolution = true) => throw new NotImplementedException();

        public Task<IEnumerable<BitmapFrame>> LoadXpsPagesAsync(string filename) => throw new NotImplementedException();
    }
}

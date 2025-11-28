using Extensions;
using PdfiumViewer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using TwainControl.Properties;

namespace TwainControl
{
    public class J2kFileHandler : ILoadFileHandler
    {
        public int GetPageCount(string filename) => 1;

        public IEnumerable<PdfCharacterInformation> GetPdfCharacters() => null;

        public bool IsValidFile(string filename)
        {
            return Path.GetExtension(filename.ToLowerInvariant()) switch
            {
                ".j2k" => true,
                _ => false,
            };
        }

        public async Task<BitmapFrame> LoadImageAsync(string filename)
        {
            return !IsValidFile(filename)
                   ? null
                   : await Task.Run(
                () =>
                {
                    using System.Drawing.Image image = new XPdfJpx(File.ReadAllBytes(filename)).decodeImage();
                    BitmapImage bitmapimage = image.ToBitmapImage(System.Drawing.Imaging.ImageFormat.Jpeg);
                    BitmapFrame bitmapFrame = Settings.Default.DefaultPictureResizeRatio != 100 ? BitmapFrame.Create(bitmapimage.Resize(Settings.Default.DefaultPictureResizeRatio / 100d)) : BitmapFrame.Create(bitmapimage);
                    bitmapFrame.Freeze();
                    return bitmapFrame;
                });
        }

        public Task<BitmapImage> LoadPdfAsync(string filename, int pageNumber) => throw new NotImplementedException();

        public Task<IEnumerable<BitmapFrame>> LoadTiffPagesAsync(string filename) => throw new NotImplementedException();

        public Task<BitmapFrame> LoadWebpImage(int decodeheight, string filename, bool fullresolution = true) => throw new NotImplementedException();

        public Task<IEnumerable<BitmapFrame>> LoadXpsPagesAsync(string filename) => throw new NotImplementedException();
    }
}
using Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using TwainControl.Properties;
using static Extensions.ExtensionMethods;

namespace TwainControl
{
    public class WebpFileHandler : ILoadFileHandler
    {
        public Task<MemoryStream> ConvertToImageStreamAsync(byte[] fileData, int pageNumber) => throw new NotImplementedException();

        public int GetPageCount(string filename) => 1;

        public bool IsValidFile(string filename) => Path.GetExtension(filename.ToLowerInvariant()) == ".webp";

        public Task<BitmapFrame> LoadImageAsync(string filename) => throw new NotImplementedException();

        public Task<IEnumerable<BitmapFrame>> LoadTiffPagesAsync(string filename) => throw new NotImplementedException();

        public BitmapFrame LoadWebpImage(int decodeheight, string filename)
        {
            if (!IsValidFile(filename))
            {
                return null;
            }
            BitmapImage main = (BitmapImage)filename.WebpDecode(true, decodeheight);
            BitmapFrame bitmapFrame = Settings.Default.DefaultPictureResizeRatio != 100 ? BitmapFrame.Create(main.Resize(Settings.Default.DefaultPictureResizeRatio / 100d)) : BitmapFrame.Create(main);
            bitmapFrame.Freeze();
            return bitmapFrame;
        }

        public Task<IEnumerable<BitmapFrame>> LoadXpsPagesAsync(string filename) => throw new NotImplementedException();
    }
}

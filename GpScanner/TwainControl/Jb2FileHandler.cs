using Extensions;
using JBig2Decoder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using TwainControl.Properties;

namespace TwainControl
{
    public class Jb2FileHandler : ILoadFileHandler
    {
        public Task<MemoryStream> ConvertToImageStreamAsync(byte[] fileData, int pageNumber) => throw new NotImplementedException();

        public int GetPageCount(string filename) => 1;

        public bool IsValidFile(string filename)
        {
            return Path.GetExtension(filename.ToLowerInvariant()) switch
            {
                ".jb2" => true,
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
                    JBIG2StreamDecoder jBIG2StreamDecoder = new();
                    BitmapImage bitmapimage = jBIG2StreamDecoder.decodeJBIG2(File.ReadAllBytes(filename)).ToBitmapImage();
                    BitmapFrame bitmapFrame = Settings.Default.DefaultPictureResizeRatio != 100 ? BitmapFrame.Create(bitmapimage.Resize(Settings.Default.DefaultPictureResizeRatio / 100d)) : BitmapFrame.Create(bitmapimage);
                    bitmapFrame.Freeze();
                    return bitmapFrame;
                });
        }

        public Task<IEnumerable<BitmapFrame>> LoadTiffPagesAsync(string filename) => throw new NotImplementedException();

        public Task<BitmapFrame> LoadWebpImage(int decodeheight, string filename, bool fullresolution = true) => throw new NotImplementedException();

        public Task<IEnumerable<BitmapFrame>> LoadXpsPagesAsync(string filename) => throw new NotImplementedException();
    }
}
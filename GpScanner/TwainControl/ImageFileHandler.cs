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
    public class ImageFileHandler : ILoadFileHandler
    {
        Task<IEnumerable<BitmapFrame>> ILoadFileHandler.LoadXpsPagesAsync(string filename) => throw new NotImplementedException();

        public Task<MemoryStream> ConvertToImageStreamAsync(byte[] fileData, int pageNumber) => throw new NotImplementedException();

        public int GetPageCount(string filename) => 1;

        public bool IsValidFile(string filename) => true;

        public async Task<BitmapFrame> LoadImageAsync(string filename)
        {
            switch (Path.GetExtension(filename.ToLowerInvariant()))
            {
                case ".jpg":
                case ".jpeg":
                case ".jfif":
                case ".jpe":
                case ".png":
                case ".gif":
                case ".bmp":
                    BitmapImage main = await ImageViewer.LoadImageAsync(filename);
                    BitmapFrame bitmapFrame = Settings.Default.DefaultPictureResizeRatio != 100 ? BitmapFrame.Create(main.Resize(Settings.Default.DefaultPictureResizeRatio / 100d)) : BitmapFrame.Create(main);
                    bitmapFrame.Freeze();
                    return bitmapFrame;
            }
            return null;
        }

        public Task<IEnumerable<BitmapFrame>> LoadTiffPagesAsync(string filename) => throw new NotImplementedException();

        public BitmapFrame LoadWebpImage(int decodeheight, string filename) => throw new NotImplementedException();
    }
}

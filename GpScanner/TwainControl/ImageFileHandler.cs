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
        public static bool CheckWithCurrentOsVersion(string compareversion)
        {
            string osversion = $"{Environment.OSVersion.Version.Major}.{Environment.OSVersion.Version.Minor}.{Environment.OSVersion.Version.Build}";
            Version current = new(osversion);
            Version compare = new(compareversion);
            return current >= compare;
        }

        Task<IEnumerable<BitmapFrame>> ILoadFileHandler.LoadXpsPagesAsync(string filename) => throw new NotImplementedException();

        public Task<MemoryStream> ConvertToImageStreamAsync(byte[] fileData, int pageNumber) => throw new NotImplementedException();

        public int GetPageCount(string filename) => 1;

        public bool IsValidFile(string filename)
        {
            return Path.GetExtension(filename.ToLowerInvariant()) switch
            {
                ".jpg" or ".jpeg" or ".jfif" or ".jpe" or ".png" or ".gif" or ".bmp" => true,
                ".heic" => CheckWithCurrentOsVersion("10.0.17134"),
                _ => false,
            };
        }

        public async Task<BitmapFrame> LoadImageAsync(string filename)
        {
            if (!IsValidFile(filename))
            {
                return null;
            }
            BitmapImage main = await ImageViewer.LoadImageAsync(filename);
            BitmapFrame bitmapFrame = Settings.Default.DefaultPictureResizeRatio != 100 ? BitmapFrame.Create(main.Resize(Settings.Default.DefaultPictureResizeRatio / 100d)) : BitmapFrame.Create(main);
            bitmapFrame.Freeze();
            return bitmapFrame;
        }

        public Task<IEnumerable<BitmapFrame>> LoadTiffPagesAsync(string filename) => throw new NotImplementedException();

        public BitmapFrame LoadWebpImage(int decodeheight, string filename) => throw new NotImplementedException();
    }
}

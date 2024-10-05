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
    public class TiffFileHandler : ILoadFileHandler
    {
        public Task<MemoryStream> ConvertToImageStreamAsync(byte[] fileData, int pageNumber) => throw new NotImplementedException();

        public int GetPageCount(string filename)
        {
            TiffBitmapDecoder decoder = new(new Uri(filename), BitmapCreateOptions.None, BitmapCacheOption.None);
            return decoder.Frames.Count;
        }

        public bool IsValidFile(string filename)
        {
            return Path.GetExtension(filename.ToLowerInvariant()) switch
            {
                ".tif" or ".tiff" => true,
                _ => false,
            };
        }
        public Task<BitmapFrame> LoadImageAsync(string filename) => throw new NotImplementedException();

        public async Task<IEnumerable<BitmapFrame>> LoadTiffPagesAsync(string filename)
        {
            if (!IsValidFile(filename))
            {
                return null;
            }
            List<BitmapFrame> frames = [];
            int pageCount = GetPageCount(filename);
            for (int i = 0; i < pageCount; i++)
            {
                try
                {
                    BitmapFrame bitmapFrame = await Task.Run(
                        () =>
                        {
                            TiffBitmapDecoder decoder = new(new Uri(filename), BitmapCreateOptions.None, BitmapCacheOption.None);
                            BitmapImage image = decoder.Frames[i].ToTiffJpegByteArray(Format.TiffRenkli).ToBitmapImage();
                            image.Freeze();
                            BitmapFrame frame = Settings.Default.DefaultPictureResizeRatio != 100 ? BitmapFrame.Create(image.Resize(Settings.Default.DefaultPictureResizeRatio / 100d)) : BitmapFrame.Create(image);
                            frame.Freeze();
                            return frame;
                        });
                    frames.Add(bitmapFrame);
                }
                catch (Exception)
                {
                }
            }
            return frames;
        }

        public BitmapFrame LoadWebpImage(int decodeheight, string filename) => throw new NotImplementedException();

        public Task<IEnumerable<BitmapFrame>> LoadXpsPagesAsync(string filename) => throw new NotImplementedException();
    }
}

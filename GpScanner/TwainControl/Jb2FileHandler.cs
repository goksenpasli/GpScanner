using Extensions;
using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using TwainControl.Properties;

namespace TwainControl
{
    public class Jb2FileHandler : ILoadFileHandler
    {
        public int GetPageCount(string filename)
        {
            PBoxJBig2 pBoxJBig2 = new(File.ReadAllBytes(filename), null);
            return pBoxJBig2.pages.Count;
        }

        public IEnumerable<PdfCharacterInformation> GetPdfCharacters() => null;

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
                    PBoxJBig2 pBoxJBig2 = new(File.ReadAllBytes(filename), null);
                    using Image image = pBoxJBig2.decodeImage();
                    BitmapImage bitmapimage = image.ToBitmapImage(ImageFormat.Tiff);
                    BitmapFrame bitmapFrame = Settings.Default.DefaultPictureResizeRatio != 100 ? BitmapFrame.Create(bitmapimage.Resize(Settings.Default.DefaultPictureResizeRatio / 100d)) : BitmapFrame.Create(bitmapimage);
                    bitmapFrame.Freeze();
                    return bitmapFrame;
                });
        }

        public Task<BitmapImage> LoadPdfAsync(string filename, int pageNumber) => throw new NotImplementedException();

        public async Task<IEnumerable<BitmapFrame>> LoadTiffPagesAsync(string filename)
        {
            if (!IsValidFile(filename))
            {
                return null;
            }
            List<BitmapFrame> frames = [];
            int pageCount = GetPageCount(filename);
            PBoxJBig2 pBoxJBig2 = new(File.ReadAllBytes(filename), null);
            for (int i = 0; i < pageCount; i++)
            {
                try
                {
                    BitmapFrame bitmapFrame = await Task.Run(
                        () =>
                        {
                            using Image image = pBoxJBig2.decodeImage(i + 1);
                            if (image != null)
                            {
                                BitmapImage bitmapimage = image.ToBitmapImage(ImageFormat.Tiff);
                                BitmapFrame bitmapFrame = Settings.Default.DefaultPictureResizeRatio != 100 ? BitmapFrame.Create(bitmapimage.Resize(Settings.Default.DefaultPictureResizeRatio / 100d)) : BitmapFrame.Create(bitmapimage);
                                bitmapFrame.Freeze();
                                return bitmapFrame;
                            }
                            return null;
                        });
                    frames.Add(bitmapFrame);
                }
                catch
                {
                }
            }
            return frames;
        }

        public Task<BitmapFrame> LoadWebpImage(int decodeheight, string filename, bool fullresolution = true) => throw new NotImplementedException();

        public Task<IEnumerable<BitmapFrame>> LoadXpsPagesAsync(string filename) => throw new NotImplementedException();
    }
}
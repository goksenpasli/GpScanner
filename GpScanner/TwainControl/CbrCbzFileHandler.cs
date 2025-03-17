using Extensions;
using PdfiumViewer;
using SevenZipExtractor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace TwainControl
{
    public class CbrCbzFileHandler : ILoadFileHandler
    {
        public int GetPageCount(string filename)
        {
            string[] cbzfilext = [".jpg", ".png", ".gif"];
            using ArchiveFile archiveFile = new(filename);
            return archiveFile?.Entries?.Count(z => z.Size > 0 && cbzfilext.Contains(Path.GetExtension(z.FileName.ToLowerInvariant()))) ?? 0;
        }

        public IEnumerable<PdfCharacterInformation> GetPdfCharacters() => null;

        public bool IsValidFile(string filename)
        {
            return Path.GetExtension(filename.ToLowerInvariant()) switch
            {
                ".cbr" or ".cbz" => true,
                _ => false,
            };
        }

        public async Task<BitmapFrame> LoadImageAsync(string filename)
        {
            if (!IsValidFile(filename))
            {
                return null;
            }
            ObservableCollection<ArchiveData> cbrFileContents = null;
            _ = await Application.Current.Dispatcher
            .InvokeAsync(
                async () =>
                {
                    CbrViewer cbrViewer = new();
                    cbrFileContents = await cbrViewer.ReadArchive(filename);
                });
            if (cbrFileContents?.Any() != true)
            {
                return null;
            }
            string extractPath = Path.Combine(Path.GetTempPath(), cbrFileContents[0].DosyaAdı);
            if (!File.Exists(extractPath))
            {
                using ArchiveFile archiveFile = new(filename);
                Entry entry = archiveFile?.Entries?.FirstOrDefault(z => z.FileName == cbrFileContents[0].DosyaAdı);
                entry?.Extract(extractPath);
            }
            BitmapFrame bitmapFrame = BitmapFrame.Create(new Uri(extractPath), BitmapCreateOptions.None, BitmapCacheOption.None);
            bitmapFrame.Freeze();
            return bitmapFrame;
        }

        public Task<BitmapImage> LoadPdfAsync(string filename, int pageNumber) => throw new NotImplementedException();

        public Task<IEnumerable<BitmapFrame>> LoadTiffPagesAsync(string filename) => throw new NotImplementedException();

        public Task<BitmapFrame> LoadWebpImage(int decodeheight, string filename, bool fullresolution = true) => throw new NotImplementedException();

        public Task<IEnumerable<BitmapFrame>> LoadXpsPagesAsync(string filename) => throw new NotImplementedException();
    }
}
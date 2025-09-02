using Extensions;
using SevenZipExtractor;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace TwainControl.Converter;

public sealed class CbrCbzFirstpageToBitmapImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string filepath
               ? Task.Run(
            () =>
            {
                if (!CbrImageViewer.IsCbrFile(filepath) || !File.Exists(filepath))
                {
                    return null;
                }
                using ArchiveFile archiveFile = new(filepath);
                Entry entry = archiveFile?.Entries?.Where(z => !z.IsFolder).OrderBy(z => z.FileName).FirstOrDefault();
                string extractpath = $"{Path.GetTempPath()}{entry.FileName}";
                if (!File.Exists(extractpath) || (Crc32.ComputeFile(extractpath) != entry?.CRC))
                {
                    entry?.Extract(extractpath);
                }
                BitmapImage bitmapFrame = BitmapFrame.Create(new Uri(extractpath), BitmapCreateOptions.None, BitmapCacheOption.None).ToBitmapImage(150);
                bitmapFrame.Freeze();
                return bitmapFrame;
            })
               : (object)null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
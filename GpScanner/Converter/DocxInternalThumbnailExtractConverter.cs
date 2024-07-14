using Extensions;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace GpScanner.Converter;

public sealed class DocxInternalThumbnailExtractConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (DesignerProperties.GetIsInDesignMode(new DependencyObject()) || value is not string filename || !File.Exists(filename))
        {
            return null;
        }
        if (new FileInfo(filename).Length == 0 || Path.GetExtension(filename.ToLowerInvariant()) is not ".docx")
        {
            return ShellIcon.GetExtensionIconBySize(filename, ShellIcon.SizeType.jumbo);
        }
        using ZipArchive archive = ZipFile.OpenRead(filename);
        ZipArchiveEntry entry = archive.Entries?.FirstOrDefault(z => z.Name == "thumbnail.emf");
        if (entry is null)
        {
            return ShellIcon.GetExtensionIconBySize(filename, ShellIcon.SizeType.jumbo);
        }
        using MemoryStream ms = new();
        entry.Open().CopyTo(ms);
        ms.Position = 0;
        using Bitmap bitmap = new(ms);
        return bitmap?.ToBitmapImage(ImageFormat.Png, 145);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
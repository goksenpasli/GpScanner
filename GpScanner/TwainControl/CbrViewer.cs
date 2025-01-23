using Extensions;
using SevenZipExtractor;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Extensions.ExtensionMethods;
using static Extensions.ShellIcon;

namespace TwainControl;

public class CbrViewer : SimpleArchiveViewer
{
    internal async Task<string> ExtractToFile(ArchiveData entry) => await ExtractToFileAsync(entry);

    internal async Task<ObservableCollection<ArchiveData>> ReadArchive(string archivePath) => await ReadArchiveContent(archivePath);

    protected override async Task<ObservableCollection<ArchiveData>> ReadArchiveContent(string ArchiveFilePath)
    {
        using ArchiveFile archive = new(ArchiveFilePath);
        if (archive is not null)
        {
            string[] cbzfilext = [".jpg", ".png", ".gif"];
            Arşivİçerik = [];
            List<Entry> list = archive.Entries?.Where(z => z.Size > 0 && cbzfilext.Contains(Path.GetExtension(z.FileName.ToLowerInvariant()))).ToList() ?? [];
            for (int i = 0; i < list.Count; i++)
            {
                Entry item = list[i];
                ExtendedArchiveData archiveData = new()
                {
                    SıkıştırılmışBoyut = (long)item.PackedSize,
                    DosyaAdı = item.FileName,
                    DosyaTipi = GetFileType(item.FileName, new SHFILEINFO()),
                    TamYol = item.FileName,
                    Boyut = (long)item.Size,
                    Oran = (float)item.PackedSize / item.Size,
                    DüzenlenmeZamanı = item.LastWriteTime.Date,
                    Crc = item.CRC.ToString("X"),
                    HostOs = item.HostOS,
                    Method = item.Method,
                    Attributes = (FileAttributes)item.Attributes,
                    Encrypted = item.IsEncrypted
                };
                Arşivİçerik.Add(archiveData);
            }
            return await Task.FromResult(Arşivİçerik);
        }
        return null;
    }
}

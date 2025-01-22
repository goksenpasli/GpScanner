using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using static Extensions.ShellIcon;

namespace Extensions;

public class CbzViewer : ArchiveViewer
{
    public async Task<string> ExtractToFile(ArchiveData entryname) => await ExtractToFileAsync(entryname);

    public Task<ObservableCollection<ArchiveData>> ReadArchive(string ArchiveFilePath) => ReadArchiveContent(ArchiveFilePath);

    protected override async Task<ObservableCollection<ArchiveData>> ReadArchiveContent(string ArchiveFilePath)
    {
        using ZipArchive archive = ZipFile.Open(ArchiveFilePath, ZipArchiveMode.Read);
        if (archive is not null)
        {
            string[] cbzfilext = [".jpg" ,".png",".gif"];
            Arşivİçerik = [];
            List<ZipArchiveEntry> list = archive.Entries?.Where(z => z.Length > 0 && cbzfilext.Contains(Path.GetExtension(z.Name.ToLowerInvariant()))).ToList() ?? [];
            TotalFilesCount = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                ZipArchiveEntry item = list[i];
                ArchiveData archiveData = new()
                {
                    SıkıştırılmışBoyut = item.CompressedLength,
                    DosyaAdı = item.Name,
                    DosyaTipi = GetFileType(item.Name, new SHFILEINFO()),
                    TamYol = item.FullName,
                    Boyut = item.Length,
                    Oran = (float)item.CompressedLength / item.Length,
                    DüzenlenmeZamanı = item.LastWriteTime.Date,
                    Crc = null
                };
                Arşivİçerik.Add(archiveData);
            }
            return await Task.FromResult(Arşivİçerik);
        }
        return null;
    }
}

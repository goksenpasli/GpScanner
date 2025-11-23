using Extensions;
using SevenZipExtractor;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Extensions.ExtensionMethods;

namespace TwainControl;

public class CbrViewer : SimpleArchiveViewer
{
    internal async Task<ObservableCollection<ArchiveData>> ReadArchive(string archivePath) => await ReadArchiveContent(archivePath);

    protected override async Task<ObservableCollection<ArchiveData>> ReadArchiveContent(string ArchiveFilePath)
    {
        using ArchiveFile archive = new(ArchiveFilePath);
        if (archive is not null)
        {
            string[] supportedImageExts = [".jpg", ".png", ".gif", ".jb2", ".webp"];
            Arşivİçerik = [];
            List<Entry> list = archive.Entries?.Where(z => z.Size > 0 && supportedImageExts.Contains(Path.GetExtension(z.FileName.ToLowerInvariant()))).ToList() ?? [];
            for (int i = 0; i < list.Count; i++)
            {
                Entry item = list[i];
                ExtendedArchiveData archiveData = new() { DosyaAdı = item.FileName };
                Arşivİçerik.Add(archiveData);
            }
            return await Task.FromResult(Arşivİçerik);
        }
        return null;
    }
}

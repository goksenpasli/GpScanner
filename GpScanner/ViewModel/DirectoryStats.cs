
namespace GpScanner.ViewModel;

public sealed class DirectoryStats
{
    public long Files { get; private set; }

    public long Items => Files + Subdirectories;

    public long Size { get; private set; }

    public long Subdirectories { get; private set; }

    public long TotalFiles { get; private set; }

    public long TotalItems => TotalFiles + TotalSubdirectories;

    public long TotalSize { get; private set; }

    public long TotalSubdirectories { get; private set; }

    public void AddDirectory(ref DirectoryStats stats)
    {
        Subdirectories++;

        TotalSubdirectories += stats.TotalSubdirectories + 1;

        TotalFiles += stats.TotalFiles;
        TotalSize += stats.TotalSize;
    }

    public void AddFile(long size)
    {
        Files++;
        TotalFiles++;

        Size += size;
        TotalSize += size;
    }
}

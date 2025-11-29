using Extensions;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TwainControl;

public class J2kImageViewer : ImageViewer
{
    public static bool IsJ2kFile(string filepath) => string.Equals(Path.GetExtension(filepath), ".j2k", StringComparison.InvariantCultureIgnoreCase);

    protected override async Task LoadImageAsync(string filepath, ImageViewer imageViewer)
    {
        if (filepath is not null && File.Exists(filepath))
        {
            switch (Path.GetExtension(filepath).ToLowerInvariant())
            {
                case ".j2k":
                    try
                    {
                        imageViewer.Sayfa = 1;
                        J2kFileHandler j2KFileHandler = new();
                        Source = await j2KFileHandler.LoadImageAsync(filepath);
                    }
                    catch
                    {
                        Source = null;
                    }
                    break;
            }
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using TwainControl;

namespace GpScanner;

public class FileService : IFileService
{
    public List<string> GetFileNames() => MainWindow.cvs?.View?.OfType<Scanner>()?.Select(z => z.FileName)?.ToList();
}

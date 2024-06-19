using System.Collections.Generic;

namespace GpScanner;

public interface IFileService
{
    List<string> GetFileNames();
}

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TwainControl;

namespace GpScanner;

public interface IFileService
{
    List<string> GetFileNames();
}

public interface IScannerService
{
    Scanner GetScanner();
}

public interface IWindowService
{
    Window GetActiveWindow();

    Window GetFirstWindow();
}

public class FileService : IFileService
{
    public List<string> GetFileNames() => MainWindow.cvs?.View?.OfType<Scanner>()?.Select(z => z.FileName)?.ToList();
}

public class ScannerService : IScannerService
{
    public Scanner GetScanner() => ToolBox.Scanner;
}

public class WindowService : IWindowService
{
    public Window GetActiveWindow() => Application.Current?.Windows?.OfType<Window>()?.SingleOrDefault(x => x.IsActive);

    public Window GetFirstWindow() => Application.Current?.Windows?.OfType<Window>()?.FirstOrDefault();
}

using TwainControl;

namespace GpScanner;

public class ScannerService : IScannerService
{
    public Scanner GetScanner() => ToolBox.Scanner;
}

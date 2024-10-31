using System.Collections.Generic;
using System.Runtime.InteropServices;
using TwainControl;

namespace GpScanner.ViewModel;

public class ScannerStrCmpLogicalComparer : Comparer<Scanner>
{
    public override int Compare(Scanner x, Scanner y) => StrCmpLogicalW(x.FileName, y.FileName);

    [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string x, string y);
}
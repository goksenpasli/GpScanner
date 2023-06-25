using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace GpScanner.ViewModel;

public class StrCmpLogicalComparer : Comparer<string>
{
    public override int Compare(string x, string y)
    {
        return StrCmpLogicalW(x, y);
    }

    [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string x, string y);
}
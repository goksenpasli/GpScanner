using System;
using System.Runtime.InteropServices;

namespace GpScanner.ViewModel;

internal static class Shutdown
{
    public static void DoExitWin(int flg)
    {
        TokPriv1Luid tp;
        IntPtr hproc = GetCurrentProcess();
        IntPtr htok = IntPtr.Zero;
        _ = OpenProcessToken(hproc, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, ref htok);
        tp.Count = 1;
        tp.Luid = 0;
        tp.Attr = SE_PRIVILEGE_ENABLED;
        _ = LookupPrivilegeValue(null, SE_SHUTDOWN_NAME, ref tp.Luid);
        _ = AdjustTokenPrivileges(htok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        _ = ExitWindowsEx(flg, 0);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct TokPriv1Luid
    {
        public int Count;

        public long Luid;

        public int Attr;
    }

    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    internal static extern bool AdjustTokenPrivileges(IntPtr htok, bool disall, ref TokPriv1Luid newst, int len,
        IntPtr prev, IntPtr relen);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    internal static extern bool ExitWindowsEx(int flg, int rea);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    internal static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    internal static extern bool LookupPrivilegeValue(string host, string name, ref long pluid);

    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    internal static extern bool OpenProcessToken(
        IntPtr h,
        int acc,
        ref IntPtr
            phtok);

    internal const int EWX_FORCE = 0x00000004;

    internal const int EWX_FORCEIFHUNG = 0x00000010;

    internal const int EWX_LOGOFF = 0x00000000;

    internal const int EWX_POWEROFF = 0x00000008;

    internal const int EWX_REBOOT = 0x00000002;

    internal const int EWX_SHUTDOWN = 0x00000001;

    internal const int SE_PRIVILEGE_ENABLED = 0x00000002;

    internal const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";

    internal const int TOKEN_ADJUST_PRIVILEGES = 0x00000020;

    internal const int TOKEN_QUERY = 0x00000008;
}
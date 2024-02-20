using System;
using System.Runtime.InteropServices;

namespace Extensions
{
    public static class FolderDialog
    {
        private const int BFFM_INITIALIZED = 1;
        private const int BFFM_SETEXPANDED = 0x046A;
        private const string FOLDERID_ComputerFolder = "{0AC0837C-BBF8-452A-850D-79D08E667CA7}";
        private const int MAX_PATH = 260;

        private delegate int BrowseCallBackProc(IntPtr hWnd, int msg, IntPtr lParam, IntPtr wParam);

        [Flags]
        private enum BIF : uint
        {
            None = 0,
            RETURNONLYFSDIRS = 0x00000001,
            DONTGOBELOWDOMAIN = 0x00000002,
            STATUSTEXT = 0x00000004,
            RETURNFSANCESTORS = 0x00000008,
            EDITBOX = 0x00000010,
            VALIDATE = 0x00000020,
            NEWDIALOGSTYLE = 0x00000040,
            USENEWUI = NEWDIALOGSTYLE | EDITBOX,
            BROWSEINCLUDEURLS = 0x00000080,
            UAHINT = 0x00000100,
            NONEWFOLDERBUTTON = 0x00000200,
            NOTRANSLATETARGETS = 0x00000400,
            BROWSEFORCOMPUTER = 0x00001000,
            BROWSEFORPRINTER = 0x00002000,
            BROWSEINCLUDEFILES = 0x00004000,
            SHAREABLE = 0x00008000,
            BROWSEFILEJUNCTIONS = 0x00010000
        }

        public static string SelectFolder(string Description, IntPtr Owner = default, string InitialPath = null)
        {
            BROWSEINFO bi = new() { Owner = Owner, Description = Description, Root = ThisPC(), Flags = (uint)(BIF.RETURNONLYFSDIRS | BIF.NEWDIALOGSTYLE), Callback = OnBrowseEvent, lParam = PIDLFromPath(InitialPath) };

            IntPtr Buffer = Marshal.AllocHGlobal(MAX_PATH * 2);
            IntPtr pidl = IntPtr.Zero;

            try
            {
                pidl = SHBrowseForFolder(ref bi);

                return pidl != IntPtr.Zero && SHGetPathFromIDList(pidl, Buffer) ? Marshal.PtrToStringUni(Buffer) : null;
            }
            finally
            {
                if (pidl != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(pidl);
                }
                if (bi.lParam != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(bi.lParam);
                }
                if (bi.Root != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(bi.Root);
                }
            }
        }

        private static int OnBrowseEvent(IntPtr hWnd, int msg, IntPtr lParam, IntPtr lpData)
        {
            switch (msg)
            {
                case BFFM_INITIALIZED:
                    _ = SendMessage(hWnd, BFFM_SETEXPANDED, IntPtr.Zero, lpData);
                    break;
            }

            return 0;
        }

        private static IntPtr PIDLFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return IntPtr.Zero;
            }

            IntPtr pidl = IntPtr.Zero;
            _ = SHILCreateFromPath(path, ref pidl, IntPtr.Zero);

            return pidl;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHBrowseForFolder(ref BROWSEINFO lpbi);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetKnownFolderIDList(Guid rfid, uint Flags, IntPtr Token, ref IntPtr pidl);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SHGetPathFromIDList(IntPtr pidl, IntPtr pszPath);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHILCreateFromPath(string pszPath, ref IntPtr pidl, IntPtr rgfInOut);

        private static IntPtr ThisPC()
        {
            IntPtr pidl = IntPtr.Zero;
            _ = SHGetKnownFolderIDList(new Guid(FOLDERID_ComputerFolder), 0, IntPtr.Zero, ref pidl);

            return pidl;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct BROWSEINFO
        {
            public IntPtr Owner;
            public IntPtr Root;
            public string SelectedPath;
            public string Description;
            public uint Flags;
            public BrowseCallBackProc Callback;
            public IntPtr lParam;
            public int Image;
        }
    }
}

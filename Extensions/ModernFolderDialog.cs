using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Extensions
{
    public static class ModernFolderDialog
    {
        private const int HRESULT_CANCELLED = unchecked((int)0x800704C7);

        public static string SelectFolder(Window owner = null, string initialPath = null, string title = null, string okButtonText = null)
        {
            List<string> result = SelectFolders(owner, initialPath, title, okButtonText, multiSelect: false);
            return result?.Count > 0 ? result[0] : null;
        }

        public static List<string> SelectFolders(Window owner = null, string initialPath = null, string title = null, string okButtonText = null, bool multiSelect = true)
        {
            IFileDialog dialog = null;
            List<string> results = [];

            try
            {
                dialog = (IFileDialog)new FileOpenDialog();

                dialog.GetOptions(out FOS options);

                options |= FOS.FOS_PICKFOLDERS | FOS.FOS_FORCEFILESYSTEM;

                if (multiSelect)
                {
                    options |= FOS.FOS_ALLOWMULTISELECT;
                }

                dialog.SetOptions(options);

                if (!string.IsNullOrWhiteSpace(title))
                {
                    dialog.SetTitle(title);
                }

                if (!string.IsNullOrWhiteSpace(okButtonText))
                {
                    dialog.SetOkButtonLabel(okButtonText);
                }

                if (!string.IsNullOrWhiteSpace(initialPath) && SHCreateItemFromParsingName(initialPath, IntPtr.Zero, typeof(IShellItem).GUID, out IShellItem folder) == 0)
                {
                    dialog.SetFolder(folder);
                }

                IntPtr hwnd = owner != null ? new WindowInteropHelper(owner).Handle : GetActiveWindow();

                int hr = dialog.Show(hwnd);

                if (hr == HRESULT_CANCELLED)
                {
                    return null;
                }

                CheckHR(hr);

                if ((options & FOS.FOS_ALLOWMULTISELECT) != 0)
                {
                    if (dialog is IFileOpenDialog multiDialog)
                    {
                        multiDialog.GetResults(out IShellItemArray array);

                        array.GetCount(out uint count);

                        for (uint i = 0; i < count; i++)
                        {
                            array.GetItemAt(i, out IShellItem item);
                            item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out string path);

                            if (!string.IsNullOrEmpty(path))
                            {
                                results.Add(path);
                            }

                            _ = Marshal.ReleaseComObject(item);
                        }

                        _ = Marshal.ReleaseComObject(array);
                    }
                }
                else
                {
                    dialog.GetResult(out IShellItem item);

                    item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out string path);

                    if (!string.IsNullOrEmpty(path))
                    {
                        results.Add(path);
                    }

                    _ = Marshal.ReleaseComObject(item);
                }

                return results;
            }
            finally
            {
                if (dialog != null)
                {
                    _ = Marshal.ReleaseComObject(dialog);
                }
            }
        }

        private static void CheckHR(int hr)
        {
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHCreateItemFromParsingName(string pszPath, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItem ppv);

        #region COM
        [ComImport]
        [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        private class FileOpenDialog
        {
        }
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("d57c7288-d4ad-4768-be02-9d969532d960")]
        private interface IFileDialog
        {
            [PreserveSig] int Show(IntPtr parent);

            void SetFileTypes();

            void SetFileTypeIndex(uint iFileType);

            void GetFileTypeIndex(out uint piFileType);

            void Advise();

            void Unadvise();

            void SetOptions(FOS fos);

            void GetOptions(out FOS pfos);

            void SetDefaultFolder(IShellItem psi);

            void SetFolder(IShellItem psi);

            void GetFolder(out IShellItem ppsi);

            void GetCurrentSelection(out IShellItem ppsi);

            void SetFileName(string pszName);

            void GetFileName(out string pszName);

            void SetTitle(string pszTitle);

            void SetOkButtonLabel(string pszText);

            void SetFileNameLabel(string pszLabel);

            void GetResult(out IShellItem ppsi);

            void AddPlace(IShellItem psi, FDAP fdap);

            void SetDefaultExtension(string pszDefaultExtension);

            void Close(int hr);

            void SetClientGuid();

            void ClearClientData();

            void SetFilter();
        }

        private interface IFileOpenDialog : IFileDialog
        {
            new void AddPlace(IShellItem psi, FDAP fdap);

            new void Advise();

            new void ClearClientData();

            new void Close(int hr);

            new void GetCurrentSelection(out IShellItem ppsi);

            new void GetFileName(out string pszName);

            new void GetFileTypeIndex(out uint piFileType);

            new void GetFolder(out IShellItem ppsi);

            new void GetOptions(out FOS pfos);

            new void GetResult(out IShellItem ppsi);

            void GetResults(out IShellItemArray ppenum);

            new void SetClientGuid();

            new void SetDefaultExtension(string pszDefaultExtension);

            new void SetDefaultFolder(IShellItem psi);

            new void SetFileName(string pszName);

            new void SetFileNameLabel(string pszLabel);

            new void SetFileTypeIndex(uint iFileType);

            new void SetFileTypes();

            new void SetFilter();

            new void SetFolder(IShellItem psi);

            new void SetOkButtonLabel(string pszText);

            new void SetOptions(FOS fos);

            new void SetTitle(string pszTitle);

            new int Show(IntPtr parent);

            new void Unadvise();
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
        private interface IShellItemArray
        {
            void BindToHandler();

            void GetPropertyStore();

            void GetPropertyDescriptionList();

            void GetAttributes();

            void GetCount(out uint pdwNumItems);

            void GetItemAt(uint dwIndex, out IShellItem ppsi);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        private interface IShellItem
        {
            void BindToHandler();

            void GetParent();

            void GetDisplayName(SIGDN sigdnName, out string ppszName);

            void GetAttributes();

            void Compare();
        }

        private enum SIGDN : uint
        {
            SIGDN_FILESYSPATH = 0x80058000
        }

        [Flags]
        private enum FOS : uint
        {
            None = 0,
            FOS_PICKFOLDERS = 0x20,
            FOS_FORCEFILESYSTEM = 0x40,
            FOS_ALLOWMULTISELECT = 0x200
        }

        private enum FDAP
        {
            FDAP_BOTTOM,
            FDAP_TOP
        }
        #endregion
    }
}
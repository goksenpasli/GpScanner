using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PdfiumViewer
{
    internal class NativeTreeView : TreeView
    {
        protected override void CreateHandle()
        {
            base.CreateHandle();
            _ = SetWindowTheme(Handle, "explorer", null);
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName,
                                                string pszSubIdList);
    }
}
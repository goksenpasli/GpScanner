using System;

namespace Tesseract.Internal.InteropDotNet
{
    internal interface ILibraryLoaderLogic
    {
        string FixUpLibraryName(string fileName);

        bool FreeLibrary(IntPtr libraryHandle);

        IntPtr GetProcAddress(IntPtr libraryHandle, string functionName);

        IntPtr LoadLibrary(string fileName);
    }
}
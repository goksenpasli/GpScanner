//  Copyright (c) 2014 Andrey Akinshin
//  Project URL: https://github.com/AndreyAkinshin/InteropDotNet
//  Distributed under the MIT License: http://opensource.org/licenses/MIT
using System;

namespace Tesseract.Internal.InteropDotNet {
    internal interface ILibraryLoaderLogic {
        string FixUpLibraryName(string fileName);

        bool FreeLibrary(IntPtr libraryHandle);

        IntPtr GetProcAddress(IntPtr libraryHandle, string functionName);

        IntPtr LoadLibrary(string fileName);
    }
}
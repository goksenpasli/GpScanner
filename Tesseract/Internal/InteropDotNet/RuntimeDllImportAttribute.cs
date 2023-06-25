using System;
using System.Runtime.InteropServices;

namespace Tesseract.Internal.InteropDotNet
{
    [ComVisible(true)]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class RuntimeDllImportAttribute : Attribute
    {
        public RuntimeDllImportAttribute(string libraryFileName)
        { LibraryFileName = libraryFileName; }

        public string LibraryFileName { get; }

        public bool BestFitMapping;

        public CallingConvention CallingConvention;

        public CharSet CharSet;

        public string EntryPoint;

        public bool SetLastError;

        public bool ThrowOnUnmappableChar;
    }
}
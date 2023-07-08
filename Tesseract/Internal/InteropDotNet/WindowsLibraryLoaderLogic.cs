using System;
using System.Runtime.InteropServices;

namespace Tesseract.Internal.InteropDotNet
{
    internal class WindowsLibraryLoaderLogic : ILibraryLoaderLogic
    {
        [DllImport("kernel32", EntryPoint = "FreeLibrary", CallingConvention = CallingConvention.Winapi, SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern bool WindowsFreeLibrary(IntPtr handle);

        private static int WindowsGetLastError() { return Marshal.GetLastWin32Error(); }

        [DllImport("kernel32", EntryPoint = "GetProcAddress", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        private static extern IntPtr WindowsGetProcAddress(IntPtr handle, string procedureName);

        [DllImport("kernel32", EntryPoint = "LoadLibrary", CallingConvention = CallingConvention.Winapi, SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern IntPtr WindowsLoadLibrary(string dllPath);

        public string FixUpLibraryName(string fileName) { return !string.IsNullOrEmpty(fileName) && !fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? $"{fileName}.dll" : fileName; }

        public bool FreeLibrary(IntPtr libraryHandle)
        {
            try
            {
                Logger.TraceInformation("Trying to free native library with handle {0} ...", libraryHandle);
                bool isSuccess = WindowsFreeLibrary(libraryHandle);
                if(isSuccess)
                {
                    Logger.TraceInformation("Successfully freed native library with handle {0}.", libraryHandle);
                } else
                {
                    Logger.TraceError("Failed to free native library with handle {0}.\r\nCheck windows event log.", libraryHandle);
                }

                return isSuccess;
            } catch(Exception e)
            {
                int lastError = WindowsGetLastError();
                Logger.TraceError("Failed to free native library with handle {0}.\r\nLast Error:{1}\r\nCheck inner exception and\\or windows event log.\r\nInner Exception: {2}", libraryHandle, lastError, e.ToString());
                return false;
            }
        }

        public IntPtr GetProcAddress(IntPtr libraryHandle, string functionName)
        {
            try
            {
                Logger.TraceInformation("Trying to load native function \"{0}\" from the library with handle {1}...", functionName, libraryHandle);
                IntPtr functionHandle = WindowsGetProcAddress(libraryHandle, functionName);
                if(functionHandle != IntPtr.Zero)
                {
                    Logger.TraceInformation("Successfully loaded native function \"{0}\", function handle = {1}.", functionName, functionHandle);
                } else
                {
                    throw new LoadLibraryException($"Failed to load native function \"{functionName}\" from library with handle  {libraryHandle}.");
                }

                return functionHandle;
            } catch(Exception e)
            {
                int lastError = WindowsGetLastError();
                throw new LoadLibraryException(
                    string.Format("Failed to load native function \"{0}\" from library with handle  {1}.\r\nLast Error:{1}\r\nCheck inner exception and\\or windows event log.\r\nInner Exception: {2}", functionName, libraryHandle, lastError, e),
                    e);
            }
        }

        public IntPtr LoadLibrary(string fileName)
        {
            IntPtr libraryHandle = IntPtr.Zero;

            try
            {
                Logger.TraceInformation("Trying to load native library \"{0}\"...", fileName);
                libraryHandle = WindowsLoadLibrary(fileName);
                if(libraryHandle != IntPtr.Zero)
                {
                    Logger.TraceInformation("Successfully loaded native library \"{0}\", handle = {1}.", fileName, libraryHandle);
                } else
                {
                    Logger.TraceError("Failed to load native library \"{0}\".\r\nCheck windows event log.", fileName);
                }
            } catch(Exception e)
            {
                int lastError = WindowsGetLastError();
                Logger.TraceError("Failed to load native library \"{0}\".\r\nLast Error:{1}\r\nCheck inner exception and\\or windows event log.\r\nInner Exception: {2}", fileName, lastError, e.ToString());
            }

            return libraryHandle;
        }
    }
}
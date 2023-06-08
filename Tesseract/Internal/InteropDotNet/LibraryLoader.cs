using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Tesseract.Internal.InteropDotNet
{
    public sealed class LibraryLoader
    {
        private LibraryLoader(ILibraryLoaderLogic logic) { this.logic = logic; }

        public bool FreeLibrary(string fileName)
        {
            fileName = FixUpLibraryName(fileName);
            lock(syncLock)
            {
                if(!IsLibraryLoaded(fileName))
                {
                    Logger.TraceWarning("Failed to free library \"{0}\" because it is not loaded", fileName);
                    return false;
                }

                if(logic.FreeLibrary(loadedAssemblies[fileName]))
                {
                    _ = loadedAssemblies.Remove(fileName);
                    return true;
                }

                return false;
            }
        }

        public IntPtr GetProcAddress(IntPtr dllHandle, string name)
        {
            IntPtr procAddress = logic.GetProcAddress(dllHandle, name);
            return procAddress == IntPtr.Zero ? throw new LoadLibraryException($"Failed to load proc {name}") : procAddress;
        }

        public bool IsLibraryLoaded(string fileName)
        {
            fileName = FixUpLibraryName(fileName);
            lock(syncLock)
            {
                return loadedAssemblies.ContainsKey(fileName);
            }
        }

        public IntPtr LoadLibrary(string fileName, string platformName = null)
        {
            fileName = FixUpLibraryName(fileName);
            lock(syncLock)
            {
                if(!loadedAssemblies.ContainsKey(fileName))
                {
                    if(platformName == null)
                    {
                        platformName = SystemManager.GetPlatformName();
                    }

                    Logger.TraceInformation($"Current platform: {platformName}");

                    IntPtr dllHandle = CheckCustomSearchPath(fileName, platformName);
                    if(dllHandle == IntPtr.Zero)
                    {
                        dllHandle = CheckExecutingAssemblyDomain(fileName, platformName);
                    }

                    if(dllHandle == IntPtr.Zero)
                    {
                        dllHandle = CheckCurrentAppDomain(fileName, platformName);
                    }

                    if(dllHandle == IntPtr.Zero)
                    {
                        dllHandle = CheckCurrentAppDomainBin(fileName, platformName);
                    }

                    if(dllHandle == IntPtr.Zero)
                    {
                        dllHandle = CheckWorkingDirecotry(fileName, platformName);
                    }

                    loadedAssemblies[fileName] = dllHandle != IntPtr.Zero
                        ? dllHandle
                        : throw new DllNotFoundException($"Failed to find library \"{fileName}\" for platform {platformName}.");
                }

                return loadedAssemblies[fileName];
            }
        }

        public string CustomSearchPath { get; set; }

        private IntPtr CheckCurrentAppDomain(string fileName, string platformName)
        {
            string baseDirectory = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
            Logger.TraceInformation("Checking current application domain location '{0}' for '{1}' on platform {2}.", baseDirectory, fileName, platformName);
            return InternalLoadLibrary(baseDirectory, platformName, fileName);
        }

        /// <summary>
        /// Special test for web applications.
        /// </summary>
        /// <remarks>
        /// Note that this makes a couple of assumptions these being: <list type="bullet"><item>That the current
        /// application domain's location for web applications corresponds to the web applications root
        /// directory.</item><item>That the tesseract\leptonica dlls reside in the corresponding x86 or x64 directories
        /// in the bin directory under the apps root directory.</item></list>
        /// </remarks>
        /// <param name="fileName"></param>
        /// <param name="platformName"></param>
        /// <returns></returns>
        private IntPtr CheckCurrentAppDomainBin(string fileName, string platformName)
        {
            string baseDirectory = Path.Combine(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory), "bin");
            if(Directory.Exists(baseDirectory))
            {
                Logger.TraceInformation("Checking current application domain's bin location '{0}' for '{1}' on platform {2}.", baseDirectory, fileName, platformName);
                return InternalLoadLibrary(baseDirectory, platformName, fileName);
            }

            Logger.TraceInformation("No bin directory exists under the current application domain's location, skipping.");
            return IntPtr.Zero;
        }

        private IntPtr CheckCustomSearchPath(string fileName, string platformName)
        {
            string baseDirectory = CustomSearchPath;
            if(!string.IsNullOrEmpty(baseDirectory))
            {
                Logger.TraceInformation("Checking custom search location '{0}' for '{1}' on platform {2}.", baseDirectory, fileName, platformName);
                return InternalLoadLibrary(baseDirectory, platformName, fileName);
            }

            Logger.TraceInformation("Custom search path is not defined, skipping.");
            return IntPtr.Zero;
        }

        private IntPtr CheckExecutingAssemblyDomain(string fileName, string platformName)
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            if(executingAssembly == null)
            {
                return IntPtr.Zero;
            }

            string baseDirectory = Path.GetDirectoryName(executingAssembly.Location);
            Logger.TraceInformation("Checking executing application domain location '{0}' for '{1}' on platform {2}.", baseDirectory, fileName, platformName);
            return InternalLoadLibrary(baseDirectory, platformName, fileName);
        }

        private IntPtr CheckWorkingDirecotry(string fileName, string platformName)
        {
            string baseDirectory = Path.GetFullPath(Environment.CurrentDirectory);
            Logger.TraceInformation("Checking working directory '{0}' for '{1}' on platform {2}.", baseDirectory, fileName, platformName);
            return InternalLoadLibrary(baseDirectory, platformName, fileName);
        }

        private string FixUpLibraryName(string fileName) { return logic.FixUpLibraryName(fileName); }

        private IntPtr InternalLoadLibrary(string baseDirectory, string platformName, string fileName)
        {
            string fullPath = Path.Combine(baseDirectory, Path.Combine(platformName, fileName));
            return File.Exists(fullPath) ? logic.LoadLibrary(fullPath) : IntPtr.Zero;
        }

        private readonly Dictionary<string, IntPtr> loadedAssemblies = new Dictionary<string, IntPtr>();

        private readonly ILibraryLoaderLogic logic;

        private readonly object syncLock = new object();

        #region Singleton
        public static LibraryLoader Instance
        {
            get
            {
                if(instance == null)
                {
                    switch(SystemManager.GetOperatingSystem())
                    {
                        case OperatingSystem.Windows:
                            Logger.TraceInformation("Current OS: Windows");
                            instance = new LibraryLoader(new WindowsLibraryLoaderLogic());
                            break;

                        case OperatingSystem.Unix:
                            Logger.TraceInformation("Current OS: Unix");
                            instance = new LibraryLoader(new UnixLibraryLoaderLogic());
                            break;

                        case OperatingSystem.MacOSX:
                            Logger.TraceInformation("Current OS: MacOsX");
                            instance = new LibraryLoader(new UnixLibraryLoaderLogic());
                            break;

                        default:
                            throw new Exception("Unsupported operation system");
                    }
                }

                return instance;
            }
        }

        private static LibraryLoader instance;
    #endregion Singleton
    }
}
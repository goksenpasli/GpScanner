using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace GpScanner.ViewModel;

public enum FileType
{
    File = 0,

    Folder = 1
}

// TODO: Check for valid method parameter input (e.g. If provided path is a directory and exists, etc..)
public static class Win32FileScanner
{
    private static readonly IntPtr invalidHandle = new(-1);

    /// <summary>
    /// Provides a enumerable of file results that contain a range of information about both files and directories
    /// discovered in the provided directory path.
    /// </summary>
    /// <param name="path">The folder path.</param>
    /// <param name="rootStats">
    /// A cumulative stat object representing total files, folder, sizes, etc for the base directory provided.
    /// </param>
    /// <param name="maxDepth">Maximum folder depth to recurse. Set -1 to disable max depth.</param>
    public static IEnumerable<FileResult> EnumerateFileItems(string path, out DirectoryStats rootStats, int maxDepth = -1)
    {
        rootStats = new DirectoryStats();
        return ScanRecursive(Path.GetFullPath(path), maxDepth, 0, rootStats);
    }

    /// <summary>
    /// A barebones version of EnumerateFiles() provides only the path and not statistics. Only provides files and not
    /// directories.
    /// </summary>
    /// <param name="maxDepth">Maximum folder depth to recurse. Set -1 to disable max depth.</param>
    public static IEnumerable<string> EnumerateFilepaths(string path, int maxDepth = -1) { return ScanRecursiveFilepath(Path.GetFullPath(path), maxDepth, 0); }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FindClose(IntPtr hFindFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindFirstFile(string lpFileName, out Win32FindData lpFindFileData);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool FindNextFile(IntPtr hFindFile, out Win32FindData lpFindFileData);

    private static long GetFilesize(Win32FindData findData) { return findData.nFileSizeLow + ((long)findData.nFileSizeHigh * uint.MaxValue); }
    private static bool IsValidFile(Win32FindData findData) { return !findData.cFileName.Equals(".") && !findData.cFileName.Equals(".."); }

    private static IEnumerable<FileResult> ScanRecursive(string path, int maxDepth, int depth, DirectoryStats parent)
    {
        IntPtr handle = invalidHandle;

        try
        {
            handle = FindFirstFile($@"{path}\*", out Win32FindData findData);

            if (handle != invalidHandle)
            {
                do
                {
                    if (findData.dwFileAttributes.HasFlag(FileAttributes.ReparsePoint | FileAttributes.Directory) || !IsValidFile(findData))
                    {
                        continue;
                    }

                    string fullPath = Path.Combine(path, findData.cFileName);

                    DateTime creationTime = ToDateTime(findData.ftCreationTime);
                    DateTime lastWriteTime = ToDateTime(findData.ftLastWriteTime);
                    DateTime lastAccessTime = ToDateTime(findData.ftLastAccessTime);

                    if (findData.dwFileAttributes.HasFlag(FileAttributes.Directory))
                    {
                        if (maxDepth >= 0 && depth + 1 > maxDepth)
                        {
                            continue;
                        }

                        DirectoryStats stats = new();

                        foreach (FileResult fileResult in ScanRecursive(fullPath, maxDepth, depth + 1, stats))
                        {
                            yield return fileResult;
                        }

                        parent.AddDirectory(ref stats);

                        yield return new FileResult(fullPath, 0, findData.dwFileAttributes, creationTime, lastWriteTime, lastAccessTime, FileType.Folder, depth, stats);
                    }
                    else
                    {
                        long filesize = GetFilesize(findData);

                        parent.AddFile(filesize);

                        yield return new FileResult(fullPath, filesize, findData.dwFileAttributes, creationTime, lastWriteTime, lastAccessTime, FileType.File, depth);
                    }
                }
                while (FindNextFile(handle, out findData));
            }
        }
        finally
        {
            _ = FindClose(handle);
        }
    }

    private static IEnumerable<string> ScanRecursiveFilepath(string path, int maxDepth, int depth)
    {
        IntPtr handle = invalidHandle;

        try
        {
            handle = FindFirstFile($@"{path}\*", out Win32FindData findData);

            if (handle != invalidHandle)
            {
                do
                {
                    if (findData.dwFileAttributes.HasFlag(FileAttributes.ReparsePoint | FileAttributes.Directory) || !IsValidFile(findData))
                    {
                        continue;
                    }

                    string fullPath = Path.Combine(path, findData.cFileName);

                    if (findData.dwFileAttributes.HasFlag(FileAttributes.Directory))
                    {
                        if (maxDepth >= 0 && depth + 1 > maxDepth)
                        {
                            continue;
                        }

                        foreach (string filePath in ScanRecursiveFilepath(fullPath, maxDepth, depth + 1))
                        {
                            yield return filePath;
                        }
                    }
                    else
                    {
                        yield return fullPath;
                    }
                }
                while (FindNextFile(handle, out findData));
            }
        }
        finally
        {
            _ = FindClose(handle);
        }
    }

    /// <summary>
    /// Converts the provided Win32 FileTime struct into a .NET DateTime struct.
    /// </summary>
    private static DateTime ToDateTime(FileTime fileTime)
    {
        byte[] highBytes = BitConverter.GetBytes(fileTime.dwHighDateTime);
        Array.Resize(ref highBytes, 8);

        long longValue = BitConverter.ToInt64(highBytes, 0);
        longValue <<= 32;
        longValue |= fileTime.dwLowDateTime;
        highBytes = null;
        return DateTime.FromFileTime(longValue);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct FileTime
    {
        public readonly uint dwLowDateTime;
        public readonly uint dwHighDateTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private readonly struct Win32FindData
    {
        public readonly FileAttributes dwFileAttributes;
        public readonly FileTime ftCreationTime;
        public readonly FileTime ftLastAccessTime;
        public readonly FileTime ftLastWriteTime;
        public readonly uint nFileSizeHigh;
        public readonly uint nFileSizeLow;
        public readonly uint dwReserved0;
        public readonly uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public readonly string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public readonly string cAlternateFileName;
    }
}

public sealed class DirectoryStats
{
    public long Files { get; private set; }

    public long Items => Files + Subdirectories;

    public long Size { get; private set; }

    public long Subdirectories { get; private set; }

    public long TotalFiles { get; private set; }

    public long TotalItems => TotalFiles + TotalSubdirectories;

    public long TotalSize { get; private set; }

    public long TotalSubdirectories { get; private set; }

    public void AddDirectory(ref DirectoryStats stats)
    {
        Subdirectories++;

        TotalSubdirectories += stats.TotalSubdirectories + 1;

        TotalFiles += stats.TotalFiles;
        TotalSize += stats.TotalSize;
    }

    public void AddFile(long size)
    {
        Files++;
        TotalFiles++;

        Size += size;
        TotalSize += size;
    }
}

public sealed class FileResult(string path, long filesize, FileAttributes attributes, DateTime creationTime, DateTime lastWriteTime, DateTime lastAccessTime, FileType type, int depth, DirectoryStats stats = null)
{
    public FileAttributes Attributes { get; } = attributes;

    public DateTime CreationTime { get; } = creationTime;

    public int Depth { get; } = depth;

    public bool IsFolder => Type == FileType.Folder;

    public DateTime LastAccessTime { get; } = lastAccessTime;

    public DateTime LastWriteTime { get; } = lastWriteTime;

    /// <summary>
    /// Gets the absolute path to this file.
    /// </summary>
    public string Path { get; } = path;

    /// <summary>
    /// Gets the size of this file in bytes.
    /// </summary>
    public long Size { get; } = filesize;

    public DirectoryStats Stats { get; } = stats;

    public FileType Type { get; } = type;
}
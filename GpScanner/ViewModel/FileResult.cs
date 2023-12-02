using System;
using System.IO;

namespace GpScanner.ViewModel;

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
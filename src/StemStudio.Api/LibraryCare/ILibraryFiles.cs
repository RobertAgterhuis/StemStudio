namespace StemStudio.Api.LibraryCare;

/// <summary>Free and total bytes of a volume.</summary>
/// <param name="FreeBytes">Bytes available to the process.</param>
/// <param name="TotalBytes">Size of the volume.</param>
public sealed record VolumeSpace(long FreeBytes, long TotalBytes);

/// <summary>
/// File-system seam of library care (013 tasks: disk full, free space and move failures are tested through it, never by
/// filling a disk).
/// </summary>
public interface ILibraryFiles
{
    /// <summary>Free and total bytes of the volume of a path.</summary>
    /// <param name="path">An existing path on the volume.</param>
    /// <returns>The space, or null when it cannot be measured.</returns>
    VolumeSpace? Space(string path);

    /// <summary>Renames a file or a directory on the same volume (the restore switch).</summary>
    /// <param name="source">Existing file or directory.</param>
    /// <param name="destination">New path (must not exist).</param>
    /// <exception cref="IOException">The move failed (open handle, permissions).</exception>
    void Move(string source, string destination);

    /// <summary>Copies a file, creating its folder and replacing an existing file (the merge restore).</summary>
    /// <param name="source">Existing file.</param>
    /// <param name="destination">Target path.</param>
    /// <exception cref="IOException">The copy failed (disk full, open handle).</exception>
    void Copy(string source, string destination);
}

/// <summary><see cref="ILibraryFiles"/> on the real file system.</summary>
public sealed class LibraryFiles : ILibraryFiles
{
    /// <inheritdoc />
    public VolumeSpace? Space(string path)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!);
            return new VolumeSpace(drive.AvailableFreeSpace, drive.TotalSize);
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Move(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (Directory.Exists(source))
        {
            Directory.Move(source, destination);
        }
        else
        {
            File.Move(source, destination);
        }
    }

    /// <inheritdoc />
    public void Copy(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }
}

/// <summary>The live database connection string (feature 013: snapshot and restore switch).</summary>
/// <param name="ConnectionString">SQLite connection string.</param>
public sealed record LiveDatabase(string ConnectionString);

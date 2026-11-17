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
}

/// <summary>The live database connection string (feature 013: snapshot and restore switch).</summary>
/// <param name="ConnectionString">SQLite connection string.</param>
public sealed record LiveDatabase(string ConnectionString);

using Microsoft.Extensions.Options;
using StemStudio.Api.Storage;

namespace StemStudio.Api.Assets;

/// <summary>Free space of a drive (replaced in tests).</summary>
public interface IDiskSpace
{
    /// <summary>Bytes available to the process on the drive of a path.</summary>
    /// <param name="path">A path on the drive.</param>
    /// <returns>Free bytes.</returns>
    long AvailableBytes(string path);
}

/// <summary><see cref="IDiskSpace"/> from <see cref="DriveInfo"/>.</summary>
public sealed class DriveDiskSpace : IDiskSpace
{
    /// <inheritdoc />
    public long AvailableBytes(string path) => new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).AvailableFreeSpace;
}

/// <summary>Refuses exports when the data drive has less than <see cref="StemStudioApiOptions.MinFreeDiskBytes"/> free (FR-015).</summary>
/// <param name="disk">Free space source.</param>
/// <param name="dataDirectory">The API data directory.</param>
/// <param name="options">API options.</param>
public sealed class DiskSpaceGuard(IDiskSpace disk, DataDirectory dataDirectory, IOptions<StemStudioApiOptions> options)
{
    /// <summary>True when an export may start.</summary>
    /// <returns>Whether enough space is free.</returns>
    public bool HasRoom() => disk.AvailableBytes(dataDirectory.Root) >= options.Value.MinFreeDiskBytes;
}

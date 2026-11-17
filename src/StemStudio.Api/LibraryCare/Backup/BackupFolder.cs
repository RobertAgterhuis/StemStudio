using Microsoft.Extensions.Options;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.LibraryCare.Backup;

/// <summary>
/// The backups folder (research R-05): created when missing, probed for writability (create and delete
/// <c>.stemstudio-write-test-&lt;guid&gt;</c>) and measured; the reason tells how to fix it.
/// </summary>
/// <param name="paths">Library-care folders.</param>
/// <param name="files">Free-space seam.</param>
/// <param name="options">API options (<c>BackupHostDirHint</c>).</param>
public sealed class BackupFolder(LibraryCarePaths paths, ILibraryFiles files, IOptions<StemStudioApiOptions> options)
{
    /// <summary>Reason of a folder that cannot be created or written.</summary>
    public const string NotWritableText =
        "The backups folder cannot be written. Check that it exists and that StemStudio may write to it (on Linux: owner 1654).";

    /// <summary>Probes the folder.</summary>
    /// <returns>Its state.</returns>
    public FolderStateDto Probe()
    {
        var hint = options.Value.BackupHostDirHint;
        if (!TryCreate() || !CanWrite())
        {
            return new FolderStateDto { Available = Directory.Exists(paths.BackupsDirectory), Writable = false, HostPathHint = hint, FreeBytes = null, TotalBytes = null, Reason = NotWritableText };
        }

        var space = files.Space(paths.BackupsDirectory);
        return new FolderStateDto { Available = true, Writable = true, HostPathHint = hint, FreeBytes = space?.FreeBytes, TotalBytes = space?.TotalBytes, Reason = null };
    }

    private bool TryCreate()
    {
        try
        {
            Directory.CreateDirectory(paths.BackupsDirectory);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private bool CanWrite()
    {
        var probe = Path.Combine(paths.BackupsDirectory, $".stemstudio-write-test-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

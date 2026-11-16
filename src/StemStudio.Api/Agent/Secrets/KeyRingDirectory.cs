using StemStudio.Agent.Model;

namespace StemStudio.Api.Agent.Secrets;

/// <summary>Paths the key ring directory is checked against (research R-04).</summary>
/// <param name="Directory">Resolved key ring directory.</param>
/// <param name="DataDir">The library (<c>StemStudio:DataDir</c>).</param>
/// <param name="BackupDir">The backups folder (<c>StemStudio:BackupDir</c>, feature 013), if configured.</param>
/// <param name="ContentRoot">The API's content root.</param>
public sealed record KeyRingLocations(string Directory, string DataDir, string? BackupDir, string ContentRoot);

/// <summary>
/// Placement guard, creation and permission check of the Data Protection key ring directory (011 follow-up claude-key,
/// CK-FR-003, CK-FR-005). Never throws: a problem becomes an unavailable <see cref="KeyRingStatus"/>.
/// </summary>
public static class KeyRingDirectory
{
    /// <summary>Mode 0700: read, write and search for the app user only.</summary>
    public const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    /// <summary>Checks the placement, creates the directory, proves it writable and restricts it to 0700 where Unix modes exist.</summary>
    /// <param name="locations">The directory and the places it must stay out of.</param>
    /// <param name="modes">Unix file modes of the platform.</param>
    /// <returns>The status.</returns>
    public static KeyRingStatus Prepare(KeyRingLocations locations, IUnixFileModes modes)
    {
        ArgumentNullException.ThrowIfNull(locations);
        ArgumentNullException.ThrowIfNull(modes);
        var directory = Path.GetFullPath(locations.Directory);
        var reason = PlacementProblem(directory, locations) ?? (IsWritable(directory) ? null : KeyStoreUnavailableReasons.NotWritable);
        return reason is null ? KeyRingStatus.Ready(directory, Restrict(directory, modes)) : KeyRingStatus.Unavailable(directory, reason);
    }

    private static string? PlacementProblem(string directory, KeyRingLocations locations)
    {
        (string? Parent, string Reason)[] forbidden =
        [
            (locations.DataDir, KeyStoreUnavailableReasons.InsideLibrary),
            (locations.BackupDir, KeyStoreUnavailableReasons.InsideBackups),
            (locations.ContentRoot, KeyStoreUnavailableReasons.InsideApp),
        ];
        var inside = forbidden.FirstOrDefault(entry => entry.Parent is { } parent && IsSameOrBelow(directory, parent));
        return inside.Reason ?? (IsInGitWorkTree(directory) ? KeyStoreUnavailableReasons.InsideRepository : null);
    }

    private static bool IsSameOrBelow(string path, string parent)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
        var candidate = Path.TrimEndingDirectorySeparator(path);
        return candidate.Equals(root, PathComparison)
            || candidate.StartsWith(root + Path.DirectorySeparatorChar, PathComparison);
    }

    private static bool IsInGitWorkTree(string directory)
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            var git = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(git) || File.Exists(git))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".write-probe-{Guid.NewGuid():N}");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static UnixFileMode? Restrict(string directory, IUnixFileModes modes)
    {
        if (!modes.Supported)
        {
            return null;
        }

        var mode = modes.GetMode(directory);
        return (mode & ~OwnerOnly) == 0 || TrySetOwnerOnly(directory, modes) ? null : mode;
    }

    private static bool TrySetOwnerOnly(string directory, IUnixFileModes modes)
    {
        try
        {
            modes.SetMode(directory, OwnerOnly);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}

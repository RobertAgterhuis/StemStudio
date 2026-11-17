using System.Globalization;

namespace StemStudio.Api.Storage;

/// <summary>The absolute API data directory (<c>StemStudio:DataDir</c>).</summary>
/// <param name="Root">Absolute path.</param>
public sealed record DataDirectory(string Root)
{
    /// <summary>Resolves a configured directory; relative paths are relative to the content root.</summary>
    /// <param name="dataDir">Configured path.</param>
    /// <param name="contentRoot">Application content root.</param>
    /// <returns>The data directory.</returns>
    public static DataDirectory Resolve(string dataDir, string contentRoot) =>
        new(Path.GetFullPath(Path.Combine(contentRoot, dataDir)));

    /// <summary>Path of a sub-directory.</summary>
    /// <param name="name">Generated directory name (never user input).</param>
    /// <returns>The absolute path.</returns>
    public string Sub(string name) => Path.Combine(Root, name);

    /// <summary>Directory of one track's files: <c>tracks/&lt;trackId&gt;</c>.</summary>
    /// <param name="trackId">Track ID.</param>
    /// <returns>The absolute path.</returns>
    public string TrackDirectory(Guid trackId) => Path.Combine(Sub("tracks"), trackId.ToString("D", CultureInfo.InvariantCulture));

    /// <summary>
    /// The absolute path of a stored data-relative path (forward slashes), or null when it would leave the data directory
    /// (<c>GetFullPath</c> + <c>StartsWith(root + separator)</c>; defence in depth for paths read from the database).
    /// </summary>
    /// <param name="relativePath">The stored path.</param>
    /// <returns>The absolute path, or null.</returns>
    public string? ContainedPath(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Root));
        var full = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? full : null;
    }

    /// <summary>The absolute path of a stored data-relative path that must stay inside the data directory.</summary>
    /// <param name="relativePath">The stored path.</param>
    /// <returns>The absolute path.</returns>
    /// <exception cref="InvalidOperationException">The path leaves the data directory.</exception>
    public string RequireContainedPath(string relativePath) =>
        ContainedPath(relativePath) ?? throw new InvalidOperationException("A stored file path points outside the data directory.");
}

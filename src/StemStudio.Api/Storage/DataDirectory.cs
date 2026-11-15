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
}

namespace StemStudio.Api.Storage;

/// <summary>Stem files under <c>&lt;DataDir&gt;/tracks/&lt;trackId&gt;/stems/&lt;name&gt;.flac</c> (ADR-004).</summary>
public interface IStemFileStore
{
    /// <summary>The path of a stem, built from the GUID and the validated stem name only.</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="stem">Known stem name.</param>
    /// <returns>Absolute path.</returns>
    /// <exception cref="ArgumentException">The stem name is unknown.</exception>
    string GetStemPath(Guid trackId, string stem);

    /// <summary>The stored stem file, or null when the name is unknown or the file is missing.</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="stem">Stem name.</param>
    /// <returns>File info or null.</returns>
    FileInfo? FindStem(Guid trackId, string stem);

    /// <summary>Writes a stem to a temporary file, then renames it into place.</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="stem">Known stem name.</param>
    /// <param name="write">Writes the content into the given stream.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The file size in bytes.</returns>
    Task<long> SaveStemAsync(Guid trackId, string stem, Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken);

    /// <summary>Deletes the track directory (<c>tracks/&lt;trackId&gt;</c>) with all stems, if present.</summary>
    /// <param name="trackId">Track ID.</param>
    /// <exception cref="IOException">A file could not be deleted.</exception>
    /// <exception cref="UnauthorizedAccessException">Access to a file was denied.</exception>
    void DeleteTrackFiles(Guid trackId);
}

using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Storage;

/// <summary>Local-disk implementation of <see cref="IStemFileStore"/>.</summary>
/// <param name="dataDirectory">The API data directory.</param>
public sealed class StemFileStore(DataDirectory dataDirectory) : IStemFileStore
{
    /// <inheritdoc />
    public string GetStemPath(Guid trackId, string stem)
    {
        if (!StemCatalog.IsKnown(stem))
        {
            throw new ArgumentException($"'{stem}' is not a stem name.", nameof(stem));
        }

        return Path.Combine(TrackDirectory(trackId), "stems", stem + ".flac");
    }

    /// <inheritdoc />
    public void DeleteTrackFiles(Guid trackId)
    {
        var directory = TrackDirectory(trackId);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <inheritdoc />
    public FileInfo? FindStem(Guid trackId, string stem)
    {
        if (!StemCatalog.IsKnown(stem))
        {
            return null;
        }

        var file = new FileInfo(GetStemPath(trackId, stem));
        return file.Exists ? file : null;
    }

    /// <inheritdoc />
    public async Task<long> SaveStemAsync(Guid trackId, string stem, Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);
        return await AtomicFile.SaveAsync(GetStemPath(trackId, stem), write, cancellationToken);
    }

    private string TrackDirectory(Guid trackId) => dataDirectory.TrackDirectory(trackId);
}

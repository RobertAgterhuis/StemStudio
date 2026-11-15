using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Api.Storage;

/// <summary>Identifies one detail stem file of a track.</summary>
/// <param name="TrackId">Track ID.</param>
/// <param name="Parent">Parent stem name.</param>
/// <param name="Name">Detail stem name allowed for the parent.</param>
public sealed record DetailStemKey(Guid TrackId, string Parent, string Name);

/// <summary>Detail stem files under <c>&lt;DataDir&gt;/tracks/&lt;trackId&gt;/detail/&lt;parent&gt;/&lt;name&gt;.flac</c> (data-model.md 5).</summary>
public interface IDetailStemFileStore
{
    /// <summary>The path of a detail stem, built from the GUID and the enum names only.</summary>
    /// <param name="key">Track, parent and name.</param>
    /// <returns>Absolute path.</returns>
    /// <exception cref="ArgumentException">The parent or name is not in the detail enums.</exception>
    string GetPath(DetailStemKey key);

    /// <summary>The stored file, or null when the names are unknown or the file is missing.</summary>
    /// <param name="key">Track, parent and name.</param>
    /// <returns>File info or null.</returns>
    FileInfo? Find(DetailStemKey key);

    /// <summary>Writes a detail stem to a temporary file, then renames it into place.</summary>
    /// <param name="key">Track, parent and name.</param>
    /// <param name="write">Writes the content into the given stream.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The file size in bytes.</returns>
    Task<long> SaveAsync(DetailStemKey key, Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken);

    /// <summary>Deletes the <c>detail/</c> folder of a track, if present.</summary>
    /// <param name="trackId">Track ID.</param>
    /// <exception cref="IOException">A file could not be deleted.</exception>
    /// <exception cref="UnauthorizedAccessException">Access to a file was denied.</exception>
    void DeleteAll(Guid trackId);
}

/// <summary>Local-disk implementation of <see cref="IDetailStemFileStore"/>.</summary>
/// <param name="dataDirectory">The API data directory.</param>
public sealed class DetailStemFileStore(DataDirectory dataDirectory) : IDetailStemFileStore
{
    /// <inheritdoc />
    public string GetPath(DetailStemKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!DetailCatalog.IsAllowed(key.Parent, key.Name))
        {
            throw new ArgumentException($"'{key.Parent}/{key.Name}' is not a detail stem.", nameof(key));
        }

        return Path.Combine(DetailDirectory(key.TrackId), key.Parent, key.Name + ".flac");
    }

    /// <inheritdoc />
    public FileInfo? Find(DetailStemKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!DetailCatalog.IsAllowed(key.Parent, key.Name))
        {
            return null;
        }

        var file = new FileInfo(GetPath(key));
        return file.Exists ? file : null;
    }

    /// <inheritdoc />
    public Task<long> SaveAsync(DetailStemKey key, Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);
        return AtomicFile.SaveAsync(GetPath(key), write, cancellationToken);
    }

    /// <inheritdoc />
    public void DeleteAll(Guid trackId)
    {
        var directory = DetailDirectory(trackId);
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private string DetailDirectory(Guid trackId) => Path.Combine(dataDirectory.TrackDirectory(trackId), "detail");
}

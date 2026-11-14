using System.Globalization;
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

        return Path.Combine(dataDirectory.Sub("tracks"), trackId.ToString("D", CultureInfo.InvariantCulture), "stems", stem + ".flac");
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
        var path = GetStemPath(trackId, stem);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return await AtomicFile.WriteAsync($"{path}.{Guid.NewGuid():N}.tmp", WriteThenMeasure, _ => path);

        async Task<long> WriteThenMeasure(FileStream stream)
        {
            await write(stream, cancellationToken);
            return stream.Length;
        }
    }
}

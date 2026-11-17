using System.Globalization;
using StemStudio.Api.Storage;

namespace StemStudio.Api.Generations;

/// <summary>Take files under <c>generations/&lt;generationId&gt;/take-&lt;n&gt;.flac</c> (paths from the GUID and the take number only).</summary>
/// <param name="data">The API data directory.</param>
public sealed class TakeStorage(DataDirectory data)
{
    /// <summary>Path of a take relative to the data directory (forward slashes).</summary>
    /// <param name="generationId">The generation.</param>
    /// <param name="index">The take number.</param>
    /// <returns>The relative path.</returns>
    public static string RelativePath(Guid generationId, int index) =>
        string.Create(CultureInfo.InvariantCulture, $"generations/{generationId:D}/take-{index}.flac");

    /// <summary>The absolute path of a relative take path.</summary>
    /// <param name="relativePath">The stored path.</param>
    /// <returns>The absolute path.</returns>
    /// <exception cref="InvalidOperationException">The stored path leaves the data directory.</exception>
    public string FullPath(string relativePath) => data.RequireContainedPath(relativePath);

    /// <summary>Writes a take atomically.</summary>
    /// <param name="relativePath">The stored path.</param>
    /// <param name="write">Writes the content.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The size in bytes.</returns>
    public Task<long> SaveAsync(string relativePath, Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken) =>
        AtomicFile.SaveAsync(FullPath(relativePath), write, cancellationToken);

    /// <summary>The file of a stored take, if it exists inside the data directory.</summary>
    /// <param name="relativePath">The stored path.</param>
    /// <returns>The file or null.</returns>
    public FileInfo? Find(string relativePath) => data.ContainedPath(relativePath) is { } path && File.Exists(path) ? new FileInfo(path) : null;

    /// <summary>Deletes every file of a generation.</summary>
    /// <param name="generationId">The generation.</param>
    public void DeleteAll(Guid generationId)
    {
        var directory = Path.Combine(data.Sub("generations"), generationId.ToString("D", CultureInfo.InvariantCulture));
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

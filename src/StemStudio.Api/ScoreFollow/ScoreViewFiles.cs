using System.Globalization;
using StemStudio.Api.Storage;

namespace StemStudio.Api.ScoreFollow;

/// <summary>
/// SVG pages of score views under <c>&lt;DataDir&gt;/assets/&lt;assetId&gt;/view/page-NNN.svg</c> (data-model 3.5). Every path is
/// built from a GUID and a page number and checked for containment in the data directory; the 007 asset deletion removes
/// the folder with the item.
/// </summary>
/// <param name="dataDirectory">The data directory.</param>
public sealed class ScoreViewFiles(DataDirectory dataDirectory)
{
    /// <summary>Path of one page relative to the data directory.</summary>
    /// <param name="assetId">Score item.</param>
    /// <param name="page">1-based page.</param>
    /// <returns>The relative path with forward slashes.</returns>
    public static string RelativePath(Guid assetId, int page) =>
        string.Create(CultureInfo.InvariantCulture, $"assets/{assetId:D}/view/page-{page:D3}.svg");

    /// <summary>Stores one page atomically.</summary>
    /// <param name="assetId">Score item.</param>
    /// <param name="page">1-based page.</param>
    /// <param name="write">Writes the SVG.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task SaveAsync(Guid assetId, int page, Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken) =>
        await AtomicFile.SaveAsync(dataDirectory.RequireContainedPath(RelativePath(assetId, page)), write, cancellationToken);

    /// <summary>The full path of a stored page, or null when it does not exist.</summary>
    /// <param name="assetId">Score item.</param>
    /// <param name="page">1-based page.</param>
    /// <returns>The path, or null.</returns>
    public string? Find(Guid assetId, int page)
    {
        var path = dataDirectory.RequireContainedPath(RelativePath(assetId, page));
        return File.Exists(path) ? path : null;
    }

    /// <summary>Removes the stored pages of a view (before it is made again).</summary>
    /// <param name="assetId">Score item.</param>
    public void Clear(Guid assetId)
    {
        var folder = Path.GetDirectoryName(dataDirectory.RequireContainedPath(RelativePath(assetId, 1)))!;
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}

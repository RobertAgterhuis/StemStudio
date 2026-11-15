using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using StemStudio.Api.Storage;

namespace StemStudio.Api.Assets;

/// <summary>Files of library items under <c>&lt;DataDir&gt;/assets/&lt;assetId&gt;/&lt;role&gt;.&lt;ext&gt;</c> (data-model 5).</summary>
public interface IAssetFileStore
{
    /// <summary>Writes a file through a temporary file, renames it into place and hashes it.</summary>
    /// <param name="assetId">Item ID.</param>
    /// <param name="fileName">File name <c>&lt;role&gt;.&lt;ext&gt;</c>.</param>
    /// <param name="write">Writes the content.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>SHA-256 and size.</returns>
    /// <exception cref="ArgumentException">The file name is not a role file name.</exception>
    Task<HashResult> SaveAsync(Guid assetId, string fileName, Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken);

    /// <summary>The stored file, or null when the name is invalid or the file is missing.</summary>
    /// <param name="assetId">Item ID.</param>
    /// <param name="fileName">File name.</param>
    /// <returns>File info or null.</returns>
    FileInfo? Find(Guid assetId, string fileName);

    /// <summary>Deletes the item's folder, if present.</summary>
    /// <param name="assetId">Item ID.</param>
    void DeleteAll(Guid assetId);
}

/// <summary>Local-disk implementation of <see cref="IAssetFileStore"/>; paths come from the GUID and validated names only.</summary>
/// <param name="dataDirectory">The API data directory.</param>
public sealed partial class AssetFileStore(DataDirectory dataDirectory) : IAssetFileStore
{
    /// <inheritdoc />
    public async Task<HashResult> SaveAsync(Guid assetId, string fileName, Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);
        var path = PathOf(assetId, fileName) ?? throw new ArgumentException($"'{fileName}' is not an asset file name.", nameof(fileName));
        await AtomicFile.SaveAsync(path, write, cancellationToken);
        await using var stored = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var hash = await SHA256.HashDataAsync(stored, cancellationToken);
        return new HashResult(Convert.ToHexStringLower(hash), stored.Length);
    }

    /// <inheritdoc />
    public FileInfo? Find(Guid assetId, string fileName) => PathOf(assetId, fileName) is { } path && File.Exists(path) ? new FileInfo(path) : null;

    /// <inheritdoc />
    public void DeleteAll(Guid assetId)
    {
        var directory = Directory(assetId);
        if (System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.Delete(directory, recursive: true);
        }
    }

    private string? PathOf(Guid assetId, string fileName) =>
        FileNamePattern().IsMatch(fileName ?? string.Empty) ? Path.Combine(Directory(assetId), fileName!) : null;

    private string Directory(Guid assetId) => Path.Combine(dataDirectory.Sub("assets"), assetId.ToString("D", CultureInfo.InvariantCulture));

    [GeneratedRegex(@"\A[a-z][a-z0-9-]{0,31}\.(flac|wav|mp3|pdf|musicxml|mid)\z", RegexOptions.CultureInvariant)]
    private static partial Regex FileNamePattern();
}

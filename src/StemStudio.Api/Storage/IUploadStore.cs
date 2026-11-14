namespace StemStudio.Api.Storage;

/// <summary>An upload stored under <c>uploads/&lt;sha256&gt;&lt;ext&gt;</c>.</summary>
/// <param name="FileName">Generated file name.</param>
/// <param name="Sha256">Lower-case hex SHA-256 of the bytes.</param>
/// <param name="SizeBytes">Size in bytes.</param>
public sealed record StoredUpload(string FileName, string Sha256, long SizeBytes);

/// <summary>Uploads kept by the API until their track completes (retry source).</summary>
public interface IUploadStore
{
    /// <summary>Streams content to disk while hashing it; the file is named after its SHA-256.</summary>
    /// <param name="content">Upload content.</param>
    /// <param name="extension">.mp3, .wav or .flac (any case).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The stored upload.</returns>
    /// <exception cref="ArgumentException">The extension is not allowed.</exception>
    /// <exception cref="UploadTooLargeException">More than 209,715,200 bytes.</exception>
    /// <exception cref="EmptyUploadException">No bytes.</exception>
    Task<StoredUpload> SaveAsync(Stream content, string extension, CancellationToken cancellationToken);

    /// <summary>The path of a stored upload.</summary>
    /// <param name="fileName">A generated name (<c>&lt;sha256&gt;&lt;ext&gt;</c>).</param>
    /// <returns>Absolute path.</returns>
    /// <exception cref="ArgumentException">The name is not a generated upload name.</exception>
    string GetPath(string fileName);

    /// <summary>True when the upload exists (false for foreign names).</summary>
    /// <param name="fileName">Generated name.</param>
    /// <returns>True when present.</returns>
    bool Exists(string fileName);

    /// <summary>Deletes an upload if present.</summary>
    /// <param name="fileName">Generated name.</param>
    void Delete(string fileName);
}

using System.Text.RegularExpressions;
using StemStudio.Contracts;

namespace StemStudio.Api.Storage;

/// <summary>Local-disk implementation of <see cref="IUploadStore"/>.</summary>
/// <param name="dataDirectory">The API data directory.</param>
/// <param name="maxBytes">Largest accepted upload (default 209,715,200 bytes).</param>
public sealed partial class UploadStore(DataDirectory dataDirectory, long maxBytes = ContractLimits.MaxUploadBytes) : IUploadStore
{
    private readonly string _directory = dataDirectory.Sub("uploads");

    /// <inheritdoc />
    public async Task<StoredUpload> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        var normalized = NormalizeExtension(extension);
        Directory.CreateDirectory(_directory);
        var temp = Path.Combine(_directory, $"{Guid.NewGuid():N}.tmp");
        var hash = await AtomicFile.WriteAsync(
            temp, file => CopyNonEmptyAsync(content, file, cancellationToken), result => Path.Combine(_directory, result.Sha256 + normalized));
        return new StoredUpload(hash.Sha256 + normalized, hash.Sha256, hash.SizeBytes);
    }

    /// <inheritdoc />
    public string GetPath(string fileName) => FileNamePattern().IsMatch(fileName)
        ? Path.Combine(_directory, fileName)
        : throw new ArgumentException($"'{fileName}' is not a stored upload.", nameof(fileName));

    /// <inheritdoc />
    public bool Exists(string fileName) => FileNamePattern().IsMatch(fileName) && File.Exists(GetPath(fileName));

    /// <inheritdoc />
    public void Delete(string fileName) => File.Delete(GetPath(fileName));

    /// <summary>True for an allowed upload extension (.mp3, .wav, .flac in any case).</summary>
    /// <param name="extension">Extension with dot.</param>
    /// <returns>True when allowed.</returns>
    public static bool IsAllowedExtension(string extension) =>
        ContractLimits.UploadExtensions.Contains(extension.ToLowerInvariant());

    private static string NormalizeExtension(string extension) => IsAllowedExtension(extension)
        ? extension.ToLowerInvariant()
        : throw new ArgumentException($"'{extension}' is not an allowed upload extension.", nameof(extension));

    private async Task<HashResult> CopyNonEmptyAsync(Stream content, Stream file, CancellationToken cancellationToken)
    {
        var hash = await HashingCopy.CopyAsync(content, file, maxBytes, cancellationToken);
        return hash.SizeBytes > 0 ? hash : throw new EmptyUploadException();
    }

    [GeneratedRegex(@"\A[0-9a-f]{64}\.(mp3|wav|flac)\z", RegexOptions.CultureInvariant)]
    private static partial Regex FileNamePattern();
}

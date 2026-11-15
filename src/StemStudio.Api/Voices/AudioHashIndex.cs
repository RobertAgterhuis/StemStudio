using System.Collections.Concurrent;
using System.Security.Cryptography;
using StemStudio.Api.Storage;
using StemStudio.Domain.Voices;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Voices;

/// <summary>
/// The exact-copy guard of voice samples (010 data-model 3.3): the file-byte SHA-256 of every stored stem, detail
/// stem, take, take vocal, export and upload is kept in <c>AudioHashes</c>. The index is filled by scanning the data
/// directory at start-up and before every check (only new or changed files are hashed), so every writer is covered.
/// It is not a similarity check.
/// </summary>
/// <param name="data">The data directory.</param>
/// <param name="hashes">The hash repository.</param>
public sealed class AudioHashIndex(DataDirectory data, IAudioHashRepository hashes)
{
    private static readonly ConcurrentDictionary<string, (long Length, DateTime Written)> Seen = new(StringComparer.Ordinal);
    private static readonly string[] Folders = ["tracks", "uploads", "generations", "assets"];

    /// <summary>Whether bytes equal to a stored file.</summary>
    /// <param name="sha256">The SHA-256 of the bytes.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when StemStudio stored such a file.</returns>
    public async Task<bool> ContainsAsync(string sha256, CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken);
        return await hashes.ContainsAsync(sha256, cancellationToken);
    }

    /// <summary>Hashes the files that are new or changed since the last scan.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var found = new List<AudioHash>();
        foreach (var file in Folders.Select(data.Sub).Where(Directory.Exists).SelectMany(folder => new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories)))
        {
            if (IsNew(file))
            {
                found.Add(new AudioHash(await HashAsync(file, cancellationToken), Kind(file), Owner(file)));
            }
        }

        if (found.Count > 0)
        {
            await hashes.AddRangeAsync(found, cancellationToken);
        }
    }

    private static bool IsNew(FileInfo file)
    {
        var stamp = (file.Length, file.LastWriteTimeUtc);
        var known = Seen.TryGetValue(file.FullName, out var seen) && seen == stamp;
        Seen[file.FullName] = stamp;
        return !known && !file.Name.EndsWith(".tmp", StringComparison.Ordinal);
    }

    private static async Task<string> HashAsync(FileInfo file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenRead();
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private AudioHashKind Kind(FileInfo file)
    {
        var relative = Path.GetRelativePath(data.Root, file.FullName).Replace('\\', '/');
        return relative.Split('/')[0] switch
        {
            "uploads" => AudioHashKind.Upload,
            "assets" => AudioHashKind.Export,
            "generations" => file.Name.StartsWith("vocal", StringComparison.Ordinal) ? AudioHashKind.TakeVocal : AudioHashKind.Take,
            _ => relative.Split('/').Length > 3 ? AudioHashKind.DetailStem : AudioHashKind.Stem,
        };
    }

    private static Guid Owner(FileInfo file)
    {
        for (var directory = file.Directory; directory is not null; directory = directory.Parent)
        {
            if (Guid.TryParse(directory.Name, out var owner))
            {
                return owner;
            }
        }

        return Guid.Empty;
    }
}

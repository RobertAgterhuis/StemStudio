using Microsoft.EntityFrameworkCore;
using StemStudio.Api.Storage;
using StemStudio.Domain.LibraryCare;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence;

namespace StemStudio.Api.LibraryCare.Storage;

/// <summary>One file of the library with its classification.</summary>
/// <param name="RelativePath">Path relative to the data directory (<c>/</c> separators).</param>
/// <param name="Bytes">Size.</param>
/// <param name="Classification">Category and entity.</param>
public sealed record WalkedFile(string RelativePath, long Bytes, StorageClassification Classification);

/// <summary>Builds the <see cref="LibraryIndex"/> of a database (live or staged).</summary>
public static class LibraryIndexReader
{
    /// <summary>Reads the ids the classifier needs.</summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The index.</returns>
    public static async Task<LibraryIndex> ReadAsync(StemStudioDbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var tracks = await context.Tracks.AsNoTracking().Select(track => new { track.Id, track.Status, track.UploadFileName }).ToListAsync(cancellationToken);
        var assets = await context.Assets.AsNoTracking().ToListAsync(cancellationToken);
        return new LibraryIndex
        {
            Tracks = tracks.Select(track => track.Id).ToHashSet(),
            Assets = assets.ToDictionary(asset => asset.Id, AssetCategories.Of),
            Generations = (await context.Generations.AsNoTracking().Select(generation => generation.Id).ToListAsync(cancellationToken)).ToHashSet(),
            VoiceProfiles = (await context.VoiceProfiles.AsNoTracking().Select(profile => profile.Id).ToListAsync(cancellationToken)).ToHashSet(),
            PendingUploads = tracks.Where(track => track.Status != TrackStatus.Completed && track.UploadFileName is not null)
                .Select(track => track.UploadFileName!).ToHashSet(StringComparer.Ordinal),
        };
    }
}

/// <summary>
/// Walks the API data directory without following links and classifies every file (research R-10, data-model 6.2);
/// library-care bookkeeping files are left out.
/// </summary>
/// <param name="data">The data directory.</param>
public sealed class LibraryWalker(DataDirectory data)
{
    private static readonly EnumerationOptions Options = new()
    {
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        IgnoreInaccessible = true,
    };

    /// <summary>Walks the data directory.</summary>
    /// <param name="index">The library index.</param>
    /// <returns>The classified files.</returns>
    public IReadOnlyList<WalkedFile> Walk(LibraryIndex index)
    {
        if (!Directory.Exists(data.Root))
        {
            return [];
        }

        var files = new List<WalkedFile>();
        foreach (var file in new DirectoryInfo(data.Root).EnumerateFiles("*", Options))
        {
            var relative = Path.GetRelativePath(data.Root, file.FullName).Replace('\\', '/');
            if (StorageClassifier.Classify(relative, index) is { } classification)
            {
                files.Add(new WalkedFile(relative, file.Length, classification));
            }
        }

        return files;
    }
}

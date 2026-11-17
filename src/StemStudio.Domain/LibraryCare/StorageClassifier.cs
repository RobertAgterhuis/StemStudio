namespace StemStudio.Domain.LibraryCare;

/// <summary>What the database knows about the library, for classifying files (data-model 6.2).</summary>
public sealed record LibraryIndex
{
    /// <summary>An empty library.</summary>
    public static LibraryIndex Empty { get; } = new();

    /// <summary>Ids of every track row.</summary>
    public IReadOnlySet<Guid> Tracks { get; init; } = new HashSet<Guid>();

    /// <summary>Archive category of every library item (<see cref="BackupCategories.ForAsset"/>).</summary>
    public IReadOnlyDictionary<Guid, BackupCategory> Assets { get; init; } = new Dictionary<Guid, BackupCategory>();

    /// <summary>Ids of every generation row.</summary>
    public IReadOnlySet<Guid> Generations { get; init; } = new HashSet<Guid>();

    /// <summary>Ids of every voice profile row.</summary>
    public IReadOnlySet<Guid> VoiceProfiles { get; init; } = new HashSet<Guid>();

    /// <summary>Upload file names referenced by tracks that did not complete.</summary>
    public IReadOnlySet<string> PendingUploads { get; init; } = new HashSet<string>(StringComparer.Ordinal);
}

/// <summary>The category of one library file.</summary>
/// <param name="Category">The storage category.</param>
/// <param name="EntityId">The owning track, item, generation or profile; null when none.</param>
public sealed record StorageClassification(StorageCategory Category, Guid? EntityId);

/// <summary>
/// Classifies data-relative paths (data-model 6.2): the layout patterns plus <c>*.tmp</c>, unreferenced uploads and folders
/// without a row as leftovers. The same classifier feeds the backup selection, the restore checks and the clean-up.
/// </summary>
public static class StorageClassifier
{
    private const string LibraryCareFolder = "library-care/";

    /// <summary>Classifies one file.</summary>
    /// <param name="relativePath">Path relative to the data directory (<c>/</c> separators).</param>
    /// <param name="index">The library index.</param>
    /// <returns>The classification, or null for library-care bookkeeping (jobs, journal, plans) that is never counted.</returns>
    public static StorageClassification? Classify(string relativePath, LibraryIndex index)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(index);
        if (relativePath.StartsWith(LibraryCareFolder, StringComparison.Ordinal))
        {
            return ClassifyLibraryCare(relativePath[LibraryCareFolder.Length..]);
        }

        if (relativePath.EndsWith(".tmp", StringComparison.Ordinal))
        {
            return Leftover(null);
        }

        if (relativePath is "stemstudio.db" or "stemstudio.db-wal" or "stemstudio.db-shm")
        {
            return new StorageClassification(StorageCategory.Database, null);
        }

        var match = LibraryLayout.Match(relativePath);
        return match is null ? Leftover(null) : ClassifyMatch(match, index);
    }

    private static StorageClassification? ClassifyLibraryCare(string path)
    {
        if (path.StartsWith("staging/", StringComparison.Ordinal))
        {
            return new StorageClassification(StorageCategory.RestoreStaging, null);
        }

        return path.StartsWith("previous/", StringComparison.Ordinal) ? new StorageClassification(StorageCategory.PreviousLibrary, null) : null;
    }

    private static StorageClassification ClassifyMatch(LayoutMatch match, LibraryIndex index) => match.Kind switch
    {
        LayoutKind.Stem => Owned(match, index.Tracks, StorageCategory.Stems),
        LayoutKind.AutoArtwork or LayoutKind.UserArtwork => Owned(match, index.Tracks, StorageCategory.Artwork),
        LayoutKind.DetailStem => Owned(match, index.Tracks, StorageCategory.DetailStems),
        LayoutKind.Upload => index.PendingUploads.Contains(match.FileName)
            ? new StorageClassification(StorageCategory.PendingUploads, null)
            : Leftover(null),
        LayoutKind.AssetFile => index.Assets.TryGetValue(match.EntityId!.Value, out var category)
            ? new StorageClassification(BackupCategories.ToStorage(category), match.EntityId)
            : Leftover(match.EntityId),
        LayoutKind.GenerationFile => Owned(match, index.Generations, StorageCategory.Generations),
        _ => Owned(match, index.VoiceProfiles, StorageCategory.VoiceProfiles),
    };

    private static StorageClassification Owned(LayoutMatch match, IReadOnlySet<Guid> rows, StorageCategory category) =>
        rows.Contains(match.EntityId!.Value) ? new StorageClassification(category, match.EntityId) : Leftover(match.EntityId);

    private static StorageClassification Leftover(Guid? entityId) => new(StorageCategory.Leftovers, entityId);
}

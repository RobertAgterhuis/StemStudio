using StemStudio.Domain.Assets;

namespace StemStudio.Domain.LibraryCare;

/// <summary>Archive categories of backup-archive 1.0.0 (data-model 3.1 and 3.2).</summary>
public enum BackupCategory
{
    /// <summary>The library database (always included).</summary>
    Database,

    /// <summary>Standard stems (always included).</summary>
    Stems,

    /// <summary>Track artwork (always included).</summary>
    Artwork,

    /// <summary>Uploads of tracks that did not complete yet (always included).</summary>
    PendingUploads,

    /// <summary>Detail stems (optional, regenerable).</summary>
    DetailStems,

    /// <summary>Audio exports and stem packs (optional).</summary>
    Exports,

    /// <summary>Sheet music (optional).</summary>
    Scores,

    /// <summary>Generations, their takes and generated library items (optional).</summary>
    Generations,

    /// <summary>Play-along takes of feature 015 (optional).</summary>
    Recordings,

    /// <summary>Voice profiles and their samples (optional).</summary>
    VoiceProfiles,
}

/// <summary>Storage overview categories (backend-api 1.12.0 <c>StorageCategory</c>): the archive categories plus regenerable data.</summary>
public enum StorageCategory
{
    /// <summary>The library database incl. <c>-wal</c> and <c>-shm</c>.</summary>
    Database,

    /// <summary>Standard stems.</summary>
    Stems,

    /// <summary>Track artwork.</summary>
    Artwork,

    /// <summary>Pending uploads.</summary>
    PendingUploads,

    /// <summary>Detail stems.</summary>
    DetailStems,

    /// <summary>Exports.</summary>
    Exports,

    /// <summary>Sheet music.</summary>
    Scores,

    /// <summary>Generations.</summary>
    Generations,

    /// <summary>Play-along takes (015).</summary>
    Recordings,

    /// <summary>Voice profiles.</summary>
    VoiceProfiles,

    /// <summary>Temporary files, unreferenced uploads and folders without a row.</summary>
    Leftovers,

    /// <summary>A restore's staging folder.</summary>
    RestoreStaging,

    /// <summary>The library kept aside by a replace restore.</summary>
    PreviousLibrary,

    /// <summary>Worker separation cache (stems and results).</summary>
    WorkerSeparationCache,

    /// <summary>Worker detail cache (finished and partial).</summary>
    WorkerDetailCache,

    /// <summary>Worker job files.</summary>
    WorkerJobFiles,

    /// <summary>Worker scratch.</summary>
    WorkerScratch,
}

/// <summary>Facts about the archive categories.</summary>
public static class BackupCategories
{
    /// <summary>Every category in archive order (data-model 4.4).</summary>
    public static IReadOnlyList<BackupCategory> All { get; } = Enum.GetValues<BackupCategory>();

    /// <summary>The categories a backup may leave out, in archive order.</summary>
    public static IReadOnlyList<BackupCategory> Optional { get; } = All.Where(category => category >= BackupCategory.DetailStems).ToArray();

    /// <summary>True for a category a backup may leave out.</summary>
    /// <param name="category">The category.</param>
    /// <returns>True when optional.</returns>
    public static bool IsOptional(BackupCategory category) => category >= BackupCategory.DetailStems;

    /// <summary>The storage category of an archive category (same name).</summary>
    /// <param name="category">The archive category.</param>
    /// <returns>The storage category.</returns>
    public static StorageCategory ToStorage(BackupCategory category) => (StorageCategory)(int)category;

    /// <summary>
    /// The archive category of a library item (data-model 3.1): scores by kind; audio by its provenance origin, compared by
    /// name so the 015 member <c>recording</c> works before the enum has it.
    /// </summary>
    /// <param name="isScore">True for a score item.</param>
    /// <param name="origin">The stored provenance origin name.</param>
    /// <returns>The category.</returns>
    public static BackupCategory ForAsset(bool isScore, string origin)
    {
        if (isScore)
        {
            return BackupCategory.Scores;
        }

        return origin.ToUpperInvariant() switch
        {
            "GENERATION" => BackupCategory.Generations,
            "RECORDING" => BackupCategory.Recordings,
            _ => BackupCategory.Exports,
        };
    }
}

/// <summary>The archive category of a library item.</summary>
public static class AssetCategories
{
    /// <summary>The archive category of an item (<see cref="BackupCategories.ForAsset"/> over its kind and provenance origin).</summary>
    /// <param name="asset">The item.</param>
    /// <returns>The category.</returns>
    public static BackupCategory Of(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return BackupCategories.ForAsset(asset.Kind == AssetKind.Score, asset.Provenance.Origin.ToString());
    }
}

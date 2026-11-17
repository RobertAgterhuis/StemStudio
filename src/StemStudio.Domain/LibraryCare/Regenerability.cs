namespace StemStudio.Domain.LibraryCare;

/// <summary>Whether StemStudio can recreate data (data-model 3.2).</summary>
public enum Regenerability
{
    /// <summary>StemStudio cannot recreate it.</summary>
    Original,

    /// <summary>One action (or automatic use) recreates it.</summary>
    Regenerable,
}

/// <summary>The regenerability of every category (one source of truth for the overview and the clean-up).</summary>
public static class Regenerabilities
{
    private static readonly HashSet<StorageCategory> RegenerableCategories =
    [
        StorageCategory.DetailStems,
        StorageCategory.Leftovers,
        StorageCategory.RestoreStaging,
        StorageCategory.WorkerSeparationCache,
        StorageCategory.WorkerDetailCache,
        StorageCategory.WorkerJobFiles,
        StorageCategory.WorkerScratch,
    ];

    /// <summary>The regenerability of a storage category.</summary>
    /// <param name="category">The category.</param>
    /// <returns>Original or regenerable.</returns>
    public static Regenerability Of(StorageCategory category) =>
        RegenerableCategories.Contains(category) ? Regenerability.Regenerable : Regenerability.Original;

    /// <summary>The regenerability of an archive category.</summary>
    /// <param name="category">The category.</param>
    /// <returns>Original or regenerable.</returns>
    public static Regenerability Of(BackupCategory category) => Of(BackupCategories.ToStorage(category));
}

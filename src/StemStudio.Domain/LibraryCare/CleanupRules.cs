namespace StemStudio.Domain.LibraryCare;

/// <summary>Kinds of clean-up actions (backend-api 1.12.0 <c>CleanupKind</c>).</summary>
public enum CleanupKind
{
    /// <summary>Detail stems of tracks (Split further brings them back).</summary>
    DetailStems,

    /// <summary>The worker's per-audio cache (rebuilt automatically).</summary>
    WorkerCache,

    /// <summary>Temporary files, unreferenced uploads and orphan folders.</summary>
    Leftovers,
}

/// <summary>Which library files a clean-up kind may remove (FR-022, SC-007): only regenerable categories, never originals.</summary>
public static class CleanupRules
{
    /// <summary>True when the clean-up kind removes library files of the category.</summary>
    /// <param name="kind">The clean-up kind.</param>
    /// <param name="category">The classified category of a library file.</param>
    /// <returns>True when removed.</returns>
    public static bool Removes(CleanupKind kind, StorageCategory category) => kind switch
    {
        CleanupKind.DetailStems => category == StorageCategory.DetailStems,
        CleanupKind.Leftovers => category == StorageCategory.Leftovers,
        _ => false,
    };
}

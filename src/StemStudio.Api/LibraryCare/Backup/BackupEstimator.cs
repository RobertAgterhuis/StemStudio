using StemStudio.Api.LibraryCare.Storage;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.LibraryCare;

namespace StemStudio.Api.LibraryCare.Backup;

/// <summary>Options of a backup as archive categories.</summary>
public static class BackupOptionsMapping
{
    /// <summary>The optional categories selected by the options.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The selected optional categories.</returns>
    public static IReadOnlySet<BackupCategory> Selected(BackupOptionsDto options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var selected = new HashSet<BackupCategory>();
        Add(selected, options.DetailStems, BackupCategory.DetailStems);
        Add(selected, options.Exports, BackupCategory.Exports);
        Add(selected, options.Scores, BackupCategory.Scores);
        Add(selected, options.Generations, BackupCategory.Generations);
        Add(selected, options.Recordings, BackupCategory.Recordings);
        Add(selected, options.VoiceProfiles, BackupCategory.VoiceProfiles);
        return selected;
    }

    /// <summary>The optional categories left out by the options.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The excluded categories.</returns>
    public static IReadOnlyCollection<BackupCategory> Excluded(BackupOptionsDto options) =>
        [.. BackupCategories.Optional.Except(Selected(options))];

    /// <summary>The contract value of a category.</summary>
    /// <param name="category">The category.</param>
    /// <returns>The DTO value.</returns>
    public static BackupCategoryDto ToDto(BackupCategory category) => (BackupCategoryDto)(int)category;

    private static void Add(HashSet<BackupCategory> selected, bool on, BackupCategory category)
    {
        if (on)
        {
            selected.Add(category);
        }
    }
}

/// <summary>Estimates a backup per category from the classified library files (data-model 4.1).</summary>
public static class BackupEstimator
{
    /// <summary>Builds the estimate.</summary>
    /// <param name="files">The classified library files.</param>
    /// <param name="options">The options.</param>
    /// <param name="folder">The backups folder state.</param>
    /// <param name="marginBytes">Free space needed above the total (FR-007).</param>
    /// <returns>The estimate; <c>enough</c> when free space covers the total plus the margin.</returns>
    public static BackupEstimateDto Estimate(IReadOnlyList<WalkedFile> files, BackupOptionsDto options, FolderStateDto folder, long marginBytes)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var selected = BackupOptionsMapping.Selected(options);
        var categories = BackupCategories.All.Select(category => Size(files, category, !BackupCategories.IsOptional(category) || selected.Contains(category))).ToList();
        var total = categories.Where(category => category.Included).Sum(category => category.Bytes);
        return new BackupEstimateDto
        {
            TotalBytes = total,
            Categories = categories,
            Folder = folder,
            Enough = folder.Writable && (folder.FreeBytes is not { } free || free >= total + marginBytes),
        };
    }

    private static CategorySizeDto Size(IReadOnlyList<WalkedFile> files, BackupCategory category, bool included)
    {
        var storage = BackupCategories.ToStorage(category);
        var matching = files.Where(file => file.Classification.Category == storage).ToList();
        return new CategorySizeDto
        {
            Category = BackupOptionsMapping.ToDto(category),
            Included = included,
            Optional = BackupCategories.IsOptional(category),
            Bytes = matching.Sum(file => file.Bytes),
            Files = matching.Count,
            Regenerability = (RegenerabilityDto)(int)Regenerabilities.Of(category),
        };
    }
}

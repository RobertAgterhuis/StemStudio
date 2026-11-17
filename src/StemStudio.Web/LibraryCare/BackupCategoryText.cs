using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Web.LibraryCare;

/// <summary>Names of the archive categories (backup-archive 1.0.0) and the texts of the library job stages.</summary>
internal static class BackupCategoryText
{
    /// <summary>Readable names by wire name.</summary>
    public static IReadOnlyDictionary<string, string> Names { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["database"] = "Database (tracks, mixes, settings)",
        ["stems"] = "Stems",
        ["artwork"] = "Artwork",
        ["pendingUploads"] = "Pending uploads",
        ["detailStems"] = "Detail stems",
        ["exports"] = "Exports",
        ["scores"] = "Sheet music",
        ["generations"] = "Generations",
        ["recordings"] = "Recordings",
        ["voiceProfiles"] = "Voice profiles",
    };

    /// <summary>The built-in text of a stage.</summary>
    /// <param name="stage">The stage.</param>
    /// <returns>For example "Writing files".</returns>
    public static string Stage(LibraryJobStageDto stage) => stage switch
    {
        LibraryJobStageDto.Snapshot => "Copying the database",
        LibraryJobStageDto.Scrub => "Preparing the database copy",
        LibraryJobStageDto.Files => "Writing files",
        LibraryJobStageDto.Manifest => "Writing the contents list",
        LibraryJobStageDto.Check or LibraryJobStageDto.Validate => "Checking the archive",
        LibraryJobStageDto.Extract => "Unpacking files",
        LibraryJobStageDto.Database => "Checking the database",
        LibraryJobStageDto.Switch => "Switching to the restored library",
        _ => "Finishing",
    };
}

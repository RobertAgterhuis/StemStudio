namespace StemStudio.Contracts.LibraryCare;

/// <summary>Estimated size of one category (<c>CategorySize</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record CategorySizeDto
{
    /// <summary>The category.</summary>
    public required BackupCategoryDto Category { get; init; }

    /// <summary>Included with the requested options.</summary>
    public required bool Included { get; init; }

    /// <summary>The category can be left out.</summary>
    public required bool Optional { get; init; }

    /// <summary>Uncompressed bytes.</summary>
    public required long Bytes { get; init; }

    /// <summary>Number of files.</summary>
    public required int Files { get; init; }

    /// <summary>Whether StemStudio can recreate it.</summary>
    public required RegenerabilityDto Regenerability { get; init; }
}

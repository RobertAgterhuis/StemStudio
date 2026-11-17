namespace StemStudio.Contracts.LibraryCare;

/// <summary>Whether an archive can be restored (<c>Compatibility</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record CompatibilityDto
{
    /// <summary>The archive can be restored by this version.</summary>
    public required bool Restorable { get; init; }

    /// <summary>Why not (&lt;= 300 characters).</summary>
    public required string? Reason { get; init; }

    /// <summary>The archive's schema is older; it is migrated in staging.</summary>
    public required bool MigrationNeeded { get; init; }
}

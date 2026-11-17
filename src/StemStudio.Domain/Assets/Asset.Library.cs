using StemStudio.Domain.Library;

namespace StemStudio.Domain.Assets;

/// <summary>Filing of a library item in the artist &gt; album &gt; song folders (feature 022, data-model 2.8).</summary>
public sealed partial class Asset
{
    /// <summary>Where the item is filed; null only before the first filing (and for items that are never filed).</summary>
    public Placement? Placement { get; private set; }

    /// <summary>The user's names for this item (default all automatic).</summary>
    public LibraryInfo LibraryInfo { get; private set; } = LibraryInfo.Automatic;

    /// <summary>The rules version of the last filing; 0 = not filed yet.</summary>
    public int FiledVersion { get; private set; }

    /// <summary>Last filing (UTC).</summary>
    public DateTime? FiledAt { get; private set; }

    /// <summary>True when filed with the current rules version.</summary>
    public bool IsFiled => Placement is not null && FiledVersion == LibraryFilingRules.Version;

    /// <summary>
    /// Files the item with the current rules version; <see cref="UpdatedAt"/> changes only when the placement changed
    /// (keeps the 007 order stable).
    /// </summary>
    /// <param name="placement">The resolved placement.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>True when the placement changed.</returns>
    public bool File(Placement placement, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(placement);
        var changed = placement != Placement;
        (Placement, FiledVersion, FiledAt) = (placement, LibraryFilingRules.Version, nowUtc);
        UpdatedAt = changed ? nowUtc : UpdatedAt;
        return changed;
    }

    /// <summary>Sets the user's names for this item (validated, trimmed); the caller re-files the item.</summary>
    /// <param name="info">Names (null members = automatic).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="DomainValidationException">A name breaks a rule.</exception>
    public void SetLibraryInfo(LibraryInfo info, DateTime nowUtc) => (LibraryInfo, UpdatedAt) = (LibraryInfoRules.Validate(info), nowUtc);
}

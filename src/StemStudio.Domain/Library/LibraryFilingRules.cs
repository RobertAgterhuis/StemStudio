using StemStudio.Domain.Assets;

namespace StemStudio.Domain.Library;

/// <summary>Filing rules of feature 022 (data-model 2.7): the rules version and which items are filed.</summary>
public static class LibraryFilingRules
{
    /// <summary>Version of the R-01/R-02 rules; a bump re-files every item once (backfill).</summary>
    public const int Version = 1;

    private const char Ellipsis = '…';

    /// <summary>
    /// True for every item that lives in a folder. Every origin that exists today is filed; recorded play-along takes
    /// (feature 015) will add their origin here and are then never filed (FR-001).
    /// </summary>
    /// <param name="provenance">The item's provenance.</param>
    /// <returns>True when the item is filed.</returns>
    public static bool IsFileable(Provenance provenance)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        return Enum.IsDefined(provenance.Origin);
    }

    /// <summary>True when the item has no current placement (never filed or filed with an older rules version).</summary>
    /// <param name="asset">The item.</param>
    /// <returns>True when the backfill must file it.</returns>
    public static bool NeedsFiling(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return IsFileable(asset.Provenance) && !asset.IsFiled;
    }

    /// <summary>A display name: control characters removed, whitespace collapsed, cut to 200 characters with an ellipsis.</summary>
    /// <param name="name">Name.</param>
    /// <returns>The display name.</returns>
    public static string Cut(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var visible = new string([.. name.Select(c => char.IsControl(c) ? ' ' : c)]);
        var collapsed = string.Join(' ', visible.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (collapsed.Length <= LibraryInfoRules.MaxNameLength)
        {
            return collapsed;
        }

        var end = LibraryInfoRules.MaxNameLength - 1;
        end -= char.IsHighSurrogate(collapsed[end - 1]) ? 1 : 0;
        return collapsed[..end].TrimEnd() + Ellipsis;
    }
}

namespace StemStudio.Domain.Library;

/// <summary>Where a filing name comes from (feature 022, data-model 2.1); precedence item &gt; track &gt; match &gt; import &gt; fallback.</summary>
public enum PlacementSource
{
    /// <summary>Set by the user for this item.</summary>
    Item,

    /// <summary>Set by the user for the item's track.</summary>
    Track,

    /// <summary>The band info match (feature 005).</summary>
    Match,

    /// <summary>The names reported by the import (lookup names, track names, provenance snapshot).</summary>
    Import,

    /// <summary>A fixed fallback label.</summary>
    Fallback,
}

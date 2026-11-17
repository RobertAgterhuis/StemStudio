namespace StemStudio.Domain.Library;

/// <summary>One level of a placement (feature 022, data-model 2.3).</summary>
public sealed record PlacementLevel
{
    /// <summary>Display name (at most 200 characters, cut with an ellipsis).</summary>
    public required string Name { get; init; }

    /// <summary>Normalised key (research R-02, at most 400 characters).</summary>
    public required string Key { get; init; }

    /// <summary>Where the name comes from.</summary>
    public required PlacementSource Source { get; init; }

    /// <summary>Builds a level from a name: cut to 200 characters, key computed from the cut name.</summary>
    /// <param name="level">Folder level.</param>
    /// <param name="name">Name.</param>
    /// <param name="source">Source.</param>
    /// <returns>The level.</returns>
    public static PlacementLevel Of(FolderLevel level, string name, PlacementSource source)
    {
        var display = LibraryFilingRules.Cut(name);
        return new PlacementLevel { Name = display, Key = LibraryKey.For(level, display), Source = source };
    }
}

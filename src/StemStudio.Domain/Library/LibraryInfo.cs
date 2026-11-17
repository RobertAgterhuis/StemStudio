namespace StemStudio.Domain.Library;

/// <summary>
/// User names for filing (feature 022, data-model 2.2), on a track (all its items) or on one item; null = automatic.
/// Values are validated by <see cref="LibraryInfoRules.Validate"/>.
/// </summary>
public sealed record LibraryInfo
{
    /// <summary>Everything automatic.</summary>
    public static LibraryInfo Automatic { get; } = new();

    /// <summary>Artist name or null.</summary>
    public string? Artist { get; init; }

    /// <summary>Album name or null.</summary>
    public string? Album { get; init; }

    /// <summary>Song name or null.</summary>
    public string? Song { get; init; }

    /// <summary>The name of a level.</summary>
    /// <param name="level">Folder level.</param>
    /// <returns>The user name or null.</returns>
    public string? For(FolderLevel level) => level switch
    {
        FolderLevel.Artist => Artist,
        FolderLevel.Album => Album,
        _ => Song,
    };
}

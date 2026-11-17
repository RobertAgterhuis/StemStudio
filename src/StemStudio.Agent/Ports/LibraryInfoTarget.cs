namespace StemStudio.Agent.Ports;

/// <summary>Scope of a library info call.</summary>
public enum LibraryInfoScope
{
    /// <summary>A track: the names used to file every item of it (<c>/api/tracks/{trackId}/library-info</c>).</summary>
    Track,

    /// <summary>One library item (<c>/api/assets/{assetId}/library-info</c>).</summary>
    Item,
}

/// <summary>The track or item whose library info set_library_info reads and changes (exactly one of <c>trackId</c>/<c>assetId</c>).</summary>
/// <param name="Scope">Track or item.</param>
/// <param name="Id">The track or item id.</param>
public sealed record LibraryInfoTarget(LibraryInfoScope Scope, Guid Id)
{
    /// <summary>The library info of a track.</summary>
    /// <param name="trackId">Track identifier.</param>
    /// <returns>The target.</returns>
    public static LibraryInfoTarget ForTrack(Guid trackId) => new(LibraryInfoScope.Track, trackId);

    /// <summary>The library info of one item.</summary>
    /// <param name="assetId">Item identifier.</param>
    /// <returns>The target.</returns>
    public static LibraryInfoTarget ForItem(Guid assetId) => new(LibraryInfoScope.Item, assetId);
}

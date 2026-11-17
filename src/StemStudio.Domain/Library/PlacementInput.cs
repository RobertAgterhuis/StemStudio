using StemStudio.Domain.Assets;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Library;

/// <summary>The facts of a track used for filing (feature 022, data-model 2.6).</summary>
/// <param name="Title">Track title (import).</param>
/// <param name="Artist">Track artist (import).</param>
/// <param name="Metadata">Band info (005): lookup names and the match.</param>
/// <param name="ArtistMbid">Matched MusicBrainz artist id.</param>
/// <param name="UploadFileName">Stored upload file name (<c>&lt;sha256&gt;&lt;ext&gt;</c>), or null when not an upload.</param>
public sealed record TrackFacts(string Title, string? Artist, TrackMetadata Metadata, Guid? ArtistMbid, string? UploadFileName = null)
{
    /// <summary>The lookup title for filing: null when it is the stored (hashed) upload name (<see cref="StoredUploadName"/>),
    /// so a library from before the 404c8031 fix does not get hash-named song folders.</summary>
    public string? LookupTitle => StoredUploadName.Usable(Metadata.LookupTitle, UploadFileName);

    /// <summary>The facts of a track.</summary>
    /// <param name="track">The track.</param>
    /// <returns>The facts.</returns>
    public static TrackFacts From(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        return new TrackFacts(track.Title, track.Artist, track.Metadata, track.ArtistId, track.UploadFileName);
    }
}

/// <summary>Input of <see cref="PlacementResolver"/> (feature 022, data-model 2.6).</summary>
public sealed record PlacementInput
{
    /// <summary>The item's provenance.</summary>
    public required Provenance Provenance { get; init; }

    /// <summary>The item's library info (user names), or null.</summary>
    public LibraryInfo? ItemInfo { get; init; }

    /// <summary>The track's library info (user names), or null (no track or track gone).</summary>
    public LibraryInfo? TrackInfo { get; init; }

    /// <summary>The source track, or null (none or deleted).</summary>
    public TrackFacts? Track { get; init; }

    /// <summary>Placement of the library item this item was made from (e.g. a score from an audio item), or null.</summary>
    public Placement? SourcePlacement { get; init; }

    /// <summary>The item's current placement: kept when its track and source item are gone (FR-008).</summary>
    public Placement? Previous { get; init; }
}

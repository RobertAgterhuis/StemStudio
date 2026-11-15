namespace StemStudio.Domain.Tracks;

/// <summary>State of the online lookup of a track (feature 005, data-model 3.4).</summary>
public enum MetadataStatus
{
    /// <summary>Never looked up (tracks from before feature 005 until first opened).</summary>
    None = 0,

    /// <summary>Scheduled; the metadata job is about to be created.</summary>
    Pending = 1,

    /// <summary>The worker's metadata job is queued or running.</summary>
    Searching = 2,

    /// <summary>Recording and artist facts were found.</summary>
    Found = 3,

    /// <summary>No reliable match; nothing is guessed.</summary>
    NotFound = 4,

    /// <summary>The lookup failed; see the reason. Retryable.</summary>
    Failed = 5,

    /// <summary>Online lookups are switched off in the worker.</summary>
    Disabled = 6,
}

/// <summary>
/// Immutable snapshot of the lookup state and its main facts (feature 005, data-model 3.2). Only <see cref="Track"/>
/// creates new snapshots through its lookup transitions.
/// </summary>
public sealed record TrackMetadata
{
    /// <summary>The state of a track that was never looked up.</summary>
    public static TrackMetadata None { get; } = new();

    /// <summary>Lookup state.</summary>
    public MetadataStatus Status { get; init; }

    /// <summary>Failure reason or warnings of the last lookup (&lt;= 1000 characters).</summary>
    public string? Reason { get; init; }

    /// <summary>The current worker metadata job (32 lower-case hex characters).</summary>
    public string? MetadataJobId { get; init; }

    /// <summary>Title used for the lookup.</summary>
    public string? LookupTitle { get; init; }

    /// <summary>Artist used for the lookup.</summary>
    public string? LookupArtist { get; init; }

    /// <summary>Album reported by the source, sent with the lookup.</summary>
    public string? LookupAlbum { get; init; }

    /// <summary>Duration sent with the lookup (seconds).</summary>
    public double? LookupDurationSeconds { get; init; }

    /// <summary>ISRC of a Spotify source, sent with the lookup.</summary>
    public string? Isrc { get; init; }

    /// <summary>True when the user corrected title or artist.</summary>
    public bool CorrectedByUser { get; init; }

    /// <summary>MusicBrainz recording id once found.</summary>
    public Guid? RecordingMbid { get; init; }

    /// <summary>Recording title once found.</summary>
    public string? RecordingTitle { get; init; }

    /// <summary>MusicBrainz release group id once found.</summary>
    public Guid? ReleaseGroupMbid { get; init; }

    /// <summary>Album (release group) title once found.</summary>
    public string? Album { get; init; }

    /// <summary>First release year of the album once found (1000..2999).</summary>
    public int? Year { get; init; }

    /// <summary>Name of the matched artist once found.</summary>
    public string? ArtistName { get; init; }

    /// <summary>Last change of the lookup state (UTC); null for <see cref="None"/>.</summary>
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>The names and facts sent with a lookup.</summary>
/// <param name="Title">Title; blank uses the track title.</param>
/// <param name="Artist">Artist, when known.</param>
/// <param name="Album">Album reported by the source.</param>
/// <param name="DurationSeconds">Duration in seconds.</param>
/// <param name="Isrc">ISRC (Spotify only).</param>
public sealed record LookupQuery(string? Title, string? Artist, string? Album = null, double? DurationSeconds = null, string? Isrc = null);

/// <summary>Outcome of a completed metadata job.</summary>
public enum LookupOutcome
{
    /// <summary>A confident match.</summary>
    Found,

    /// <summary>No confident match.</summary>
    NotFound,

    /// <summary>Lookups are switched off.</summary>
    Disabled,
}

/// <summary>The facts of a completed metadata job applied to a track.</summary>
/// <param name="Outcome">The outcome; facts are kept only for <see cref="LookupOutcome.Found"/>.</param>
public sealed record LookupResult(LookupOutcome Outcome)
{
    /// <summary>MusicBrainz recording id.</summary>
    public Guid? RecordingMbid { get; init; }

    /// <summary>Recording title.</summary>
    public string? RecordingTitle { get; init; }

    /// <summary>MusicBrainz release group id.</summary>
    public Guid? ReleaseGroupMbid { get; init; }

    /// <summary>Album title.</summary>
    public string? Album { get; init; }

    /// <summary>First release year.</summary>
    public int? Year { get; init; }

    /// <summary>MusicBrainz artist id (the <c>Artists</c> row).</summary>
    public Guid? ArtistMbid { get; init; }

    /// <summary>Artist name.</summary>
    public string? ArtistName { get; init; }

    /// <summary>Non-fatal warnings joined into one text, or null.</summary>
    public string? Warnings { get; init; }
}

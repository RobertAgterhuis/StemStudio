using System.Text.Json.Serialization;

namespace StemStudio.Api.Worker;

/// <summary>Body of <c>POST /v1/metadata-jobs</c> (contract <c>CreateMetadataJobRequest</c>).</summary>
public sealed record WorkerMetadataJobRequest
{
    /// <summary>Title (1..500 characters).</summary>
    public required string Title { get; init; }

    /// <summary>Artist, when known.</summary>
    public string? Artist { get; init; }

    /// <summary>Album, when known.</summary>
    public string? Album { get; init; }

    /// <summary>Duration in seconds, when known.</summary>
    public double? DurationSeconds { get; init; }

    /// <summary>ISRC (Spotify only).</summary>
    public string? Isrc { get; init; }

    /// <summary>The standard job, for correlation only.</summary>
    public string? StandardJobId { get; init; }
}

/// <summary>Metadata job state (contract <c>MetadataJobState</c>).</summary>
public enum WorkerMetadataJobState
{
    /// <summary>Waiting in the metadata lane.</summary>
    Queued,

    /// <summary>Looking up.</summary>
    Running,

    /// <summary>Terminal: result available.</summary>
    Completed,

    /// <summary>Terminal: see <see cref="WorkerMetadataJob.Error"/>.</summary>
    Failed,
}

/// <summary>A metadata job (contract <c>MetadataJob</c>).</summary>
public sealed record WorkerMetadataJob
{
    /// <summary>Job ID (32 lower-case hex characters).</summary>
    public required string MetadataJobId { get; init; }

    /// <summary>State.</summary>
    public required WorkerMetadataJobState Status { get; init; }

    /// <summary>Human-readable stage.</summary>
    public string? Stage { get; init; }

    /// <summary>Readable failure reason.</summary>
    public string? Error { get; init; }
}

/// <summary>Outcome of a metadata job (contract <c>MetadataResult.outcome</c>).</summary>
public enum WorkerMetadataOutcome
{
    /// <summary>A confident match.</summary>
    Found,

    /// <summary>No confident match.</summary>
    [JsonStringEnumMemberName("not-found")]
    NotFound,

    /// <summary>Lookups are switched off.</summary>
    Disabled,
}

/// <summary>Result of a completed metadata job (contract <c>MetadataResult</c>).</summary>
public sealed record WorkerMetadataResult
{
    /// <summary>Outcome.</summary>
    public required WorkerMetadataOutcome Outcome { get; init; }

    /// <summary>The matched recording.</summary>
    public WorkerRecordingMatch? Recording { get; init; }

    /// <summary>Its release group.</summary>
    public WorkerReleaseGroup? ReleaseGroup { get; init; }

    /// <summary>The Cover Art Archive front, when found.</summary>
    public WorkerArtworkInfo? Artwork { get; init; }

    /// <summary>Artist facts.</summary>
    public WorkerArtistInfo? Artist { get; init; }

    /// <summary>Non-fatal warnings (at most 5).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>A matched MusicBrainz recording (contract <c>RecordingMatch</c>).</summary>
/// <param name="Mbid">Recording id.</param>
/// <param name="Title">Recording title.</param>
/// <param name="LengthSeconds">Length, when known.</param>
/// <param name="Score">Match score 0..100.</param>
/// <param name="MatchedBy"><c>isrc</c> or <c>search</c>.</param>
public sealed record WorkerRecordingMatch(Guid Mbid, string Title, double? LengthSeconds, int Score, string MatchedBy);

/// <summary>A MusicBrainz release group (contract <c>ReleaseGroupInfo</c>).</summary>
/// <param name="Mbid">Release group id.</param>
/// <param name="Title">Album title.</param>
/// <param name="PrimaryType">Primary type, e.g. Album.</param>
/// <param name="FirstReleaseYear">First release year.</param>
public sealed record WorkerReleaseGroup(Guid Mbid, string Title, string? PrimaryType, int? FirstReleaseYear);

/// <summary>MusicBrainz artist type (contract <c>ArtistType</c>).</summary>
public enum WorkerArtistType
{
    /// <summary>A group.</summary>
    Group,

    /// <summary>A person.</summary>
    Person,

    /// <summary>An orchestra.</summary>
    Orchestra,

    /// <summary>A choir.</summary>
    Choir,

    /// <summary>A character.</summary>
    Character,

    /// <summary>Other.</summary>
    Other,
}

/// <summary>A member of a group (contract <c>ArtistMember</c>).</summary>
/// <param name="Name">Name.</param>
/// <param name="FromYear">Start year.</param>
/// <param name="ToYear">End year.</param>
public sealed record WorkerArtistMember(string Name, int? FromYear = null, int? ToYear = null);

/// <summary>A Wikipedia excerpt with attribution data (contract <c>WikipediaSummary</c>).</summary>
public sealed record WorkerWikipediaSummary
{
    /// <summary>Wiki language.</summary>
    public required string Language { get; init; }

    /// <summary>Article title.</summary>
    public required string Title { get; init; }

    /// <summary>The excerpt, unmodified.</summary>
    public required string Extract { get; init; }

    /// <summary>Article URL.</summary>
    public required string PageUrl { get; init; }

    /// <summary>History URL.</summary>
    public required string HistoryUrl { get; init; }

    /// <summary>Article revision.</summary>
    public string? RevisionId { get; init; }
}

/// <summary>Artist facts (contract <c>ArtistInfo</c>).</summary>
public sealed record WorkerArtistInfo
{
    /// <summary>MusicBrainz artist id.</summary>
    public required Guid Mbid { get; init; }

    /// <summary>Name.</summary>
    public required string Name { get; init; }

    /// <summary>Type.</summary>
    public WorkerArtistType? Type { get; init; }

    /// <summary>Country code.</summary>
    public string? Country { get; init; }

    /// <summary>Start year.</summary>
    public int? BeginYear { get; init; }

    /// <summary>End year.</summary>
    public int? EndYear { get; init; }

    /// <summary>Genres.</summary>
    public IReadOnlyList<string> Genres { get; init; } = [];

    /// <summary>Members.</summary>
    public IReadOnlyList<WorkerArtistMember> Members { get; init; } = [];

    /// <summary>Website.</summary>
    public string? Website { get; init; }

    /// <summary>Wikidata item id.</summary>
    public string? WikidataId { get; init; }

    /// <summary>Wikipedia excerpt.</summary>
    public WorkerWikipediaSummary? Summary { get; init; }

    /// <summary>When the facts were fetched.</summary>
    public DateTimeOffset FetchedAt { get; init; }
}

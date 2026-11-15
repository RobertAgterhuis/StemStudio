namespace StemStudio.Domain.Tracks;

/// <summary>Online lookup of a track (feature 005, data-model 3.2 and state machine 3.4).</summary>
public sealed partial class Track
{
    /// <summary>Failure text used when the worker gives no reason.</summary>
    public const string LookupFailedDefault = "Band information could not be retrieved; try again later.";

    /// <summary>Lowest stored album year.</summary>
    public const int MinYear = 1000;

    /// <summary>Highest stored album year.</summary>
    public const int MaxYear = 2999;

    /// <summary>The lookup state and its main facts; <see cref="TrackMetadata.None"/> until a lookup is scheduled.</summary>
    public TrackMetadata Metadata { get; private set; } = TrackMetadata.None;

    /// <summary>The matched artist (<c>Artists.Mbid</c>) while the lookup is found; null otherwise.</summary>
    public Guid? ArtistId { get; private set; }

    /// <summary>True while the lookup is pending or searching (driven by the metadata synchroniser).</summary>
    public bool IsLookupActive => Metadata.Status is MetadataStatus.Pending or MetadataStatus.Searching;

    /// <summary>True unless the worker is searching (FR-009: retry and correction).</summary>
    public bool CanRetryLookup => Metadata.Status != MetadataStatus.Searching;

    /// <summary>Schedules the automatic lookup once (none -> pending); ignored after any lookup was scheduled.</summary>
    /// <param name="query">Names and facts reported by the source.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void ScheduleLookup(LookupQuery query, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (Metadata.Status != MetadataStatus.None)
        {
            return;
        }

        SetMetadata(WithQuery(TrackMetadata.None, query) with { Status = MetadataStatus.Pending }, nowUtc);
    }

    /// <summary>Schedules the lookup of a track imported before feature 005 with its own title, artist and duration (FR-012).</summary>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void ScheduleOwnLookup(DateTime nowUtc) => ScheduleLookup(new LookupQuery(Title, Artist, null, DurationSeconds), nowUtc);

    /// <summary>Retries the lookup, optionally with corrected names (stored, <see cref="TrackMetadata.CorrectedByUser"/>).</summary>
    /// <param name="title">Corrected title, or null to keep the current one.</param>
    /// <param name="artist">Corrected artist, or null to keep the current one.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="InvalidOperationException">The worker is searching.</exception>
    public void RetryLookup(string? title, string? artist, DateTime nowUtc)
    {
        if (!CanRetryLookup)
        {
            throw new InvalidOperationException($"The lookup of track {Id} is running.");
        }

        var current = Metadata.Status == MetadataStatus.None ? WithQuery(Metadata, new LookupQuery(Title, Artist, null, DurationSeconds)) : Metadata;
        var retried = Corrected(current, title, artist) with { Status = MetadataStatus.Pending, MetadataJobId = null, Reason = null };
        SetMetadata(retried, nowUtc);
    }

    /// <summary>Records the worker metadata job of a pending lookup (pending -> searching).</summary>
    /// <param name="metadataJobId">Job ID (32 lower-case hex characters).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void StartLookup(string metadataJobId, DateTime nowUtc)
    {
        if (!JobIdPattern().IsMatch(metadataJobId))
        {
            throw new ArgumentException($"'{metadataJobId}' is not a worker job ID.", nameof(metadataJobId));
        }

        RequireLookup(MetadataStatus.Pending);
        SetMetadata(Metadata with { Status = MetadataStatus.Searching, MetadataJobId = metadataJobId }, nowUtc);
    }

    /// <summary>Applies the result of the metadata job (searching -> found, notFound or disabled); facts only when found.</summary>
    /// <param name="result">The result.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void CompleteLookup(LookupResult result, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(result);
        RequireLookup(MetadataStatus.Searching);
        var facts = result.Outcome == LookupOutcome.Found ? result : new LookupResult(result.Outcome) { Warnings = result.Warnings };
        ArtistId = facts.ArtistMbid;
        SetMetadata(WithFacts(Metadata, facts) with { Status = StatusOf(result.Outcome) }, nowUtc);
    }

    /// <summary>Fails a pending or searching lookup with a readable reason.</summary>
    /// <param name="reason">Reason (blank becomes <see cref="LookupFailedDefault"/>, cut to 1000 characters).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void FailLookup(string reason, DateTime nowUtc)
    {
        if (!IsLookupActive)
        {
            throw new InvalidOperationException($"Track {Id} has no pending or searching lookup.");
        }

        SetMetadata(Metadata with { Status = MetadataStatus.Failed, Reason = Text.TrimToLength(reason, MaxErrorLength) ?? LookupFailedDefault }, nowUtc);
    }

    private TrackMetadata WithQuery(TrackMetadata metadata, LookupQuery query) => metadata with
    {
        LookupTitle = Text.TrimToLength(query.Title, MaxTitleLength) ?? Title,
        LookupArtist = Text.TrimToLength(query.Artist, MaxArtistLength),
        LookupAlbum = Text.TrimToLength(query.Album, MaxTitleLength),
        LookupDurationSeconds = query.DurationSeconds,
        Isrc = Text.TrimToLength(query.Isrc, 12),
    };

    private static TrackMetadata Corrected(TrackMetadata metadata, string? title, string? artist)
    {
        var (newTitle, newArtist) = (Text.TrimToLength(title, MaxTitleLength), Text.TrimToLength(artist, MaxArtistLength));
        if (newTitle is null && newArtist is null)
        {
            return metadata;
        }

        return metadata with { LookupTitle = newTitle ?? metadata.LookupTitle, LookupArtist = newArtist ?? metadata.LookupArtist, CorrectedByUser = true, Isrc = null };
    }

    private static TrackMetadata WithFacts(TrackMetadata metadata, LookupResult facts) => metadata with
    {
        Reason = Text.TrimToLength(facts.Warnings, MaxErrorLength),
        RecordingMbid = facts.RecordingMbid,
        RecordingTitle = Text.TrimToLength(facts.RecordingTitle, MaxTitleLength),
        ReleaseGroupMbid = facts.ReleaseGroupMbid,
        Album = Text.TrimToLength(facts.Album, MaxTitleLength),
        Year = facts.Year is >= MinYear and <= MaxYear ? facts.Year : null,
        ArtistName = Text.TrimToLength(facts.ArtistName, MaxArtistLength),
    };

    private static MetadataStatus StatusOf(LookupOutcome outcome) => outcome switch
    {
        LookupOutcome.Found => MetadataStatus.Found,
        LookupOutcome.NotFound => MetadataStatus.NotFound,
        _ => MetadataStatus.Disabled,
    };

    private void RequireLookup(MetadataStatus expected)
    {
        if (Metadata.Status != expected)
        {
            throw new InvalidOperationException($"The lookup of track {Id} is {Metadata.Status}, not {expected}.");
        }
    }

    private void SetMetadata(TrackMetadata metadata, DateTime nowUtc) => (Metadata, UpdatedAt) = (metadata with { UpdatedAt = nowUtc }, nowUtc);
}

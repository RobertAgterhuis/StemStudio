using StemStudio.Api.Worker;
using StemStudio.Domain.Artists;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Metadata;

/// <summary>Maps worker lookup data to domain inputs (feature 005).</summary>
public static class MetadataMapper
{
    /// <summary>The lookup query of a standard job's source metadata: the cleaned lookup names when reported, else title and
    /// artist. An upload without a tag title looks up its original file name: the worker only sees the stored, hashed file
    /// name, which it reports as title and lookup title (it would name the 022 song folder and search for the hash).</summary>
    /// <param name="track">The track.</param>
    /// <param name="metadata">Reported source metadata.</param>
    /// <param name="durationSeconds">Duration to use when the metadata has none (e.g. the completed result's duration).</param>
    /// <returns>The query.</returns>
    public static LookupQuery ToQuery(Track track, WorkerSourceMetadata metadata, double? durationSeconds = null)
    {
        ArgumentNullException.ThrowIfNull(track);
        var query = ToQuery(metadata, durationSeconds);
        var hashedName = track.SourceType == SourceType.Upload && (metadata.LookupTitle is null || metadata.LookupTitle == metadata.Title);
        return hashedName ? query with { Title = track.Title } : query;
    }

    private static LookupQuery ToQuery(WorkerSourceMetadata metadata, double? durationSeconds)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new LookupQuery(
            metadata.LookupTitle ?? metadata.Title,
            metadata.LookupArtist ?? metadata.Artist,
            metadata.Album,
            metadata.DurationSeconds ?? durationSeconds,
            metadata.Isrc);
    }

    /// <summary>Title and artist of a track from reported source metadata; an upload reports no artist, so its tag artist is used (keeps the 009 style guard aware of it).</summary>
    /// <param name="metadata">Reported source metadata.</param>
    /// <returns>The title and artist.</returns>
    public static SourceMetadata ToSource(WorkerSourceMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new SourceMetadata(metadata.Title, metadata.Artist ?? metadata.LookupArtist);
    }

    /// <summary>The worker-reported title and artist for <paramref name="track"/>: an upload keeps the title of its
    /// original file name (the worker only sees the stored, hashed file name), so its title is not reported.</summary>
    /// <param name="track">The track.</param>
    /// <param name="metadata">Reported source metadata.</param>
    /// <returns>The title (null keeps the current one) and artist.</returns>
    public static SourceMetadata ToSource(Track track, WorkerSourceMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(track);
        var source = ToSource(metadata);
        return track.SourceType == SourceType.Upload ? source with { Title = null } : source;
    }

    /// <summary>The metadata job request of a pending lookup; a stored hashed upload name as lookup title (tracks from
    /// before 404c8031) is replaced by the track's own title (<see cref="StoredUploadName"/>).</summary>
    /// <param name="track">A track whose lookup is pending.</param>
    /// <returns>The request.</returns>
    public static WorkerMetadataJobRequest ToRequest(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        var metadata = track.Metadata;
        return new WorkerMetadataJobRequest
        {
            Title = StoredUploadName.Usable(metadata.LookupTitle, track.UploadFileName) ?? track.Title,
            Artist = metadata.LookupArtist,
            Album = metadata.LookupAlbum,
            DurationSeconds = metadata.LookupDurationSeconds,
            Isrc = metadata.Isrc,
            StandardJobId = track.WorkerJobId,
        };
    }

    /// <summary>The domain result of a completed metadata job; warnings are joined with a space.</summary>
    /// <param name="result">The worker result.</param>
    /// <returns>The lookup result.</returns>
    public static LookupResult ToResult(WorkerMetadataResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var outcome = new LookupResult(Enum.Parse<LookupOutcome>(result.Outcome.ToString()))
        {
            Warnings = result.Warnings.Count == 0 ? null : string.Join(' ', result.Warnings),
        };
        return WithArtist(WithReleaseGroup(WithRecording(outcome, result.Recording), result.ReleaseGroup), result.Artist);
    }

    /// <summary>The artist facts of a worker result (validated by <see cref="Artist"/>).</summary>
    /// <param name="artist">The worker's artist.</param>
    /// <returns>The facts.</returns>
    public static ArtistFacts ToFacts(WorkerArtistInfo artist)
    {
        ArgumentNullException.ThrowIfNull(artist);
        var facts = new ArtistFacts
        {
            Mbid = artist.Mbid,
            Name = artist.Name,
            Type = artist.Type is { } type ? Enum.Parse<ArtistType>(type.ToString()) : null,
            Country = artist.Country,
            BeginYear = artist.BeginYear,
            EndYear = artist.EndYear,
            FetchedAt = artist.FetchedAt.UtcDateTime,
        };
        return WithDetails(facts, artist);
    }

    private static ArtistFacts WithDetails(ArtistFacts facts, WorkerArtistInfo artist) => facts with
    {
        Genres = artist.Genres,
        Members = artist.Members.Select(member => new ArtistMember(member.Name, member.FromYear, member.ToYear)).ToList(),
        Website = artist.Website,
        WikidataId = artist.WikidataId,
        Summary = ToSummary(artist.Summary),
    };

    private static LookupResult WithRecording(LookupResult result, WorkerRecordingMatch? recording) =>
        recording is null ? result : result with { RecordingMbid = recording.Mbid, RecordingTitle = recording.Title };

    private static LookupResult WithReleaseGroup(LookupResult result, WorkerReleaseGroup? group) =>
        group is null ? result : result with { ReleaseGroupMbid = group.Mbid, Album = group.Title, Year = group.FirstReleaseYear };

    private static LookupResult WithArtist(LookupResult result, WorkerArtistInfo? artist) =>
        artist is null ? result : result with { ArtistMbid = artist.Mbid, ArtistName = artist.Name };

    private static WikipediaSummary? ToSummary(WorkerWikipediaSummary? summary) => summary is null
        ? null
        : new WikipediaSummary(summary.Language, summary.Title, summary.Extract, summary.PageUrl, summary.HistoryUrl, summary.RevisionId);
}

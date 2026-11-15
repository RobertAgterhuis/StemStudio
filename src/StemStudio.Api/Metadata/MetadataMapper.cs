using StemStudio.Api.Worker;
using StemStudio.Domain.Artists;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Metadata;

/// <summary>Maps worker lookup data to domain inputs (feature 005).</summary>
public static class MetadataMapper
{
    /// <summary>The lookup query of a standard job's source metadata: the cleaned lookup names when reported, else title and artist.</summary>
    /// <param name="metadata">Reported source metadata.</param>
    /// <param name="durationSeconds">Duration to use when the metadata has none (e.g. the completed result's duration).</param>
    /// <returns>The query.</returns>
    public static LookupQuery ToQuery(WorkerSourceMetadata metadata, double? durationSeconds = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new LookupQuery(
            metadata.LookupTitle ?? metadata.Title,
            metadata.LookupArtist ?? metadata.Artist,
            metadata.Album,
            metadata.DurationSeconds ?? durationSeconds,
            metadata.Isrc);
    }

    /// <summary>The metadata job request of a pending lookup.</summary>
    /// <param name="track">A track whose lookup is pending.</param>
    /// <returns>The request.</returns>
    public static WorkerMetadataJobRequest ToRequest(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        var metadata = track.Metadata;
        return new WorkerMetadataJobRequest
        {
            Title = metadata.LookupTitle ?? track.Title,
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

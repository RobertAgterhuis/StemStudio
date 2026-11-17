using StemStudio.Api.Tracks;
using StemStudio.Api.Worker;
using StemStudio.Contracts.Lyrics;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Lyrics;

/// <summary>Maps lyrics between the domain, the worker requests and the contract DTO (feature 006, data-model 3.3).</summary>
public static class LyricsMapper
{
    /// <summary>The lookup request of a track: the 005 cleaned lookup names, else the track's own title and artist; a stored
    /// hashed upload name as lookup title counts as absent (<see cref="StoredUploadName"/>).</summary>
    /// <param name="track">The track.</param>
    /// <returns>The request.</returns>
    public static WorkerLyricsLookupRequest ToLookupRequest(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        return new WorkerLyricsLookupRequest
        {
            Title = StoredUploadName.Usable(track.Metadata.LookupTitle, track.UploadFileName) ?? track.Title,
            Artist = track.Metadata.LookupArtist ?? track.Artist,
            Album = track.Metadata.LookupAlbum,
            DurationSeconds = track.LyricsDurationSeconds ?? 0,
            StandardJobId = track.WorkerJobId,
        };
    }

    /// <summary>The domain outcome of a worker outcome.</summary>
    /// <param name="outcome">The worker outcome.</param>
    /// <returns>The domain outcome.</returns>
    public static LyricsOutcome ToOutcome(WorkerLyricsOutcome outcome) => Enum.Parse<LyricsOutcome>(outcome.ToString());

    /// <summary>Maps the lyrics of a track; a track without a row has status none.</summary>
    /// <param name="lyrics">The row, or null.</param>
    /// <returns>The DTO.</returns>
    public static LyricsDto ToDto(TrackLyrics? lyrics) => lyrics is null ? new LyricsDto { Status = LyricsStatusDto.None } : WithDocument(lyrics, new LyricsDto
    {
        Status = ToDto(lyrics.Status),
        Reason = lyrics.Reason,
        OffsetMs = lyrics.OffsetMs,
        Alignment = AlignmentOf(lyrics),
    });

    /// <summary>Maps a status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The DTO value.</returns>
    public static LyricsStatusDto ToDto(LyricsStatus status) => Enum.Parse<LyricsStatusDto>(status.ToString());

    private static LyricsDto WithDocument(TrackLyrics lyrics, LyricsDto dto) => lyrics.Document is not { } document ? dto : dto with
    {
        SyncLevel = Enum.Parse<SyncLevelDto>(document.SyncLevel.ToString()),
        Origin = Enum.Parse<LyricsOriginDto>(document.Origin.ToString()),
        Language = document.Language,
        Source = Enum.Parse<LyricsSourceDto>(document.Source.ToString()),
        Credit = LyricsCredit.For(document),
        Instrumental = document.Instrumental,
        Lines = document.Lines.Select(ToDto).ToList(),
    };

    private static LyricsLineDto ToDto(LyricsLine line) => new(line.StartMs, line.EndMs, line.Text, line.Words.Select(ToDto).ToList());

    private static LyricsWordDto ToDto(LyricsWord word) =>
        new(word.Text, word.StartMs, word.EndMs, word.Origin is { } origin ? Enum.Parse<WordOriginDto>(origin.ToString()) : null, word.LowConfidence);

    private static LyricsAlignmentDto? AlignmentOf(TrackLyrics lyrics) => lyrics.Status is LyricsStatus.AlignPending or LyricsStatus.Aligning
        ? new LyricsAlignmentDto(lyrics.AlignEstimateSeconds, TrackMapper.ToDto(lyrics.AlignDevice), lyrics.AlignProgress)
        : null;
}

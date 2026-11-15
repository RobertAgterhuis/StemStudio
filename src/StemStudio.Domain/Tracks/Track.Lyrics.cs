using StemStudio.Domain.Lyrics;

namespace StemStudio.Domain.Tracks;

/// <summary>
/// Lyrics of a track (feature 006): <see cref="Lyrics"/> is the <c>TrackLyrics</c> row (loaded only for a single
/// track), <see cref="LyricsStatus"/> its status, mirrored into the <c>Tracks.LyricsStatus</c> column for list DTOs. A
/// row exists exactly when the status is not none.
/// </summary>
public sealed partial class Track
{
    private LyricsStatus _lyricsStatus;

    /// <summary>The lyrics status: the loaded row's status, else the stored column.</summary>
    public LyricsStatus LyricsStatus
    {
        get => Lyrics?.Status ?? _lyricsStatus;
        private set => _lyricsStatus = value;
    }

    /// <summary>The lyrics row; null when the status is none or the row was not loaded.</summary>
    public TrackLyrics? Lyrics { get; private set; }

    /// <summary>Duration used for the lyrics lookup: the 005 lookup duration, else the track's duration.</summary>
    public double? LyricsDurationSeconds => Metadata.LookupDurationSeconds ?? DurationSeconds;

    /// <summary>The lyrics row, created (status none) for a track that has none yet.</summary>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The row.</returns>
    /// <exception cref="InvalidOperationException">The track has lyrics but the row was not loaded.</exception>
    public TrackLyrics EnsureLyrics(DateTime nowUtc)
    {
        if (Lyrics is null && _lyricsStatus != LyricsStatus.None)
        {
            throw new InvalidOperationException($"The lyrics of track {Id} are {_lyricsStatus} but were not loaded.");
        }

        return Lyrics ??= TrackLyrics.Create(Id, nowUtc);
    }

    /// <summary>Starts the automatic lyrics lookup of a track that never had one, once its duration is known (FR-001, FR-013).</summary>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void ScheduleLyricsLookup(DateTime nowUtc)
    {
        if (LyricsStatus == LyricsStatus.None && LyricsDurationSeconds is > 0)
        {
            EnsureLyrics(nowUtc).ScheduleAutomatic(nowUtc);
        }
    }
}

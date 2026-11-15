using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tests.Tracks;

namespace StemStudio.Domain.Tests.Lyrics;

/// <summary>Self-written lyrics documents (no third-party text) for the feature 006 domain tests.</summary>
internal static class LyricsTestData
{
    public const string JobId = "9a4c6e8b0d2f41a3b5c7d9e1f3a5b7c9";
    public static readonly DateTime T0 = DetailTestData.T0;

    public static LyricsDocument Word(LyricsSource source = LyricsSource.Lrclib, LyricsOrigin origin = LyricsOrigin.Source, WordOrigin wordOrigin = WordOrigin.Source) => new()
    {
        SyncLevel = SyncLevel.Word,
        Origin = origin,
        Language = "en",
        Source = source,
        SourceId = "1000001",
        SourceDurationSeconds = 213.0,
        Lines =
        [
            new LyricsLine(10950, 13200, "Fake line one", [W("Fake", 10950, 11400, wordOrigin), W("line", 11400, 11900, wordOrigin), W("one", 11900, 13200, wordOrigin)]),
            new LyricsLine(14000, 16000, "La la la", [W("La", 14000, 14500, wordOrigin), W("la", 14500, 15000, wordOrigin), W("la", 15000, 16000, wordOrigin)]),
        ],
        PlainText = "Fake line one\nLa la la",
    };

    public static LyricsDocument Line() => Word(origin: LyricsOrigin.Interpolated, wordOrigin: WordOrigin.Interpolated) with { SyncLevel = SyncLevel.Line };

    public static LyricsDocument Aligned() => Word(origin: LyricsOrigin.Aligned, wordOrigin: WordOrigin.Aligned);

    public static LyricsDocument Plain(LyricsSource source = LyricsSource.Lrclib) => new()
    {
        SyncLevel = SyncLevel.Plain,
        Origin = LyricsOrigin.None,
        Language = "en",
        Source = source,
        SourceId = source == LyricsSource.Lrclib ? "1000004" : null,
        Lines =
        [
            new LyricsLine(null, null, "Fake line one", [P("Fake"), P("line"), P("one")]),
            new LyricsLine(null, null, "La la la", [P("La"), P("la"), P("la")]),
        ],
        PlainText = "Fake line one\nLa la la",
    };

    public static LyricsDocument Instrumental() => new()
    {
        SyncLevel = SyncLevel.Plain,
        Origin = LyricsOrigin.None,
        Source = LyricsSource.Lrclib,
        SourceId = "1000007",
        Instrumental = true,
        Lines = [],
        PlainText = string.Empty,
    };

    public static Track CompletedTrack() => DetailTestData.CompletedTrack();

    /// <summary>A completed track whose lyrics are in the given state (reached through the public transitions).</summary>
    public static Track TrackWith(LyricsStatus status)
    {
        var track = CompletedTrack();
        var lyrics = track.EnsureLyrics(T0);
        Reach(lyrics, status);
        return track;
    }

    private static void Reach(TrackLyrics lyrics, LyricsStatus status)
    {
        if (status == LyricsStatus.None)
        {
            return;
        }

        if (status == LyricsStatus.Removed)
        {
            lyrics.Remove(T0);
            return;
        }

        lyrics.Search(T0);
        ReachFromSearching(lyrics, status);
    }

    private static void ReachFromSearching(TrackLyrics lyrics, LyricsStatus status)
    {
        switch (status)
        {
            case LyricsStatus.Found:
                lyrics.ApplyLookup(LyricsOutcome.Found, Word(), T0);
                break;
            case LyricsStatus.Instrumental:
                lyrics.ApplyLookup(LyricsOutcome.Instrumental, Instrumental(), T0);
                break;
            case LyricsStatus.NotFound:
                lyrics.ApplyLookup(LyricsOutcome.NotFound, null, T0);
                break;
            case LyricsStatus.Disabled:
                lyrics.ApplyLookup(LyricsOutcome.Disabled, null, T0);
                break;
            default:
                ReachWithText(lyrics, status);
                break;
        }
    }

    private static void ReachWithText(TrackLyrics lyrics, LyricsStatus status)
    {
        if (status == LyricsStatus.Searching)
        {
            return;
        }

        lyrics.ApplyLookup(LyricsOutcome.Found, Plain(), T0);
        if (status == LyricsStatus.Aligning)
        {
            lyrics.StartAlign(T0);
        }
        else if (status == LyricsStatus.Failed)
        {
            lyrics.StartAlign(T0);
            lyrics.Fail("The lyrics could not be aligned to the vocals.", T0);
        }
    }

    private static LyricsWord W(string text, int start, int end, WordOrigin origin) => new(text, start, end, origin, false);

    private static LyricsWord P(string text) => new(text, null, null, null, false);
}

using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Tests.InstrumentViews;

/// <summary>Chord charts, lyrics and grids for the instrument view tests.</summary>
internal static class ChartData
{
    public static ChordSegment Segment(int startMs, int endMs, string label)
    {
        HarteLabel.TryParse(label, out var chord).ShouldBeTrue();
        return new ChordSegment { StartMs = startMs, EndMs = endMs, Label = label, Root = chord.Root, Quality = chord.Quality, Bass = chord.Bass, Confidence = 0.9 };
    }

    /// <summary>A chart in C major from consecutive (label, length) pairs starting at 0.</summary>
    public static ChordChart Chart(params (string Label, int LengthMs)[] chords)
    {
        var segments = new List<ChordSegment>();
        var at = 0;
        foreach (var (label, length) in chords)
        {
            segments.Add(Segment(at, at + length, label));
            at += length;
        }

        return new ChordChart
        {
            Engine = "fake",
            Vocabulary = ChordVocabulary.Simple,
            Key = new SongKey(0, KeyMode.Major, 0.9, null),
            KeyRegions = [new KeyRegion(0, Math.Max(at, 1), 0, KeyMode.Major, 0.9)],
            Segments = segments,
        };
    }

    public static LyricsWord Word(string text, int? startMs) => new(text, startMs, startMs + 400, startMs is null ? null : WordOrigin.Source, false);

    public static LyricsLine Line(int startMs, int endMs, params (string Text, int? StartMs)[] words) =>
        new(startMs, endMs, string.Join(' ', words.Select(word => word.Text)), words.Select(word => Word(word.Text, word.StartMs)).ToList());

    public static LyricsDocument LyricsOf(SyncLevel level, params LyricsLine[] lines) =>
        new() { SyncLevel = level, Origin = LyricsOrigin.Source, Source = LyricsSource.Lrclib, Lines = lines };

    /// <summary>A reliable 120 bpm 4/4 grid of <paramref name="bars"/> bars (bar = 2 s).</summary>
    public static BeatGrid Grid(int bars)
    {
        var beats = Enumerable.Range(0, bars * 4).Select(beat => beat * 0.5).ToList();
        return new BeatGrid { Bpm = 120, Beats = beats, Downbeats = beats.Where((_, index) => index % 4 == 0).ToList(), Confidence = 0.9, Reliable = true };
    }
}

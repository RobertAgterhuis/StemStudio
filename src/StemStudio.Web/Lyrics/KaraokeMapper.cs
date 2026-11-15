using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Audio;

namespace StemStudio.Web.Lyrics;

/// <summary>Turns backend lyrics into the engine's render subset (engine contract 2.1.0: only word- and line-synced documents).</summary>
public static class KaraokeMapper
{
    /// <summary>The lyrics to attach, or null when the banner cannot be drawn (not found, plain text, a word without a time).</summary>
    /// <param name="lyrics">The backend lyrics, or null before they are loaded.</param>
    /// <returns>The engine lyrics, or null.</returns>
    public static KaraokeLyrics? ToKaraoke(LyricsDto? lyrics) =>
        lyrics is { Status: LyricsStatusDto.Found, SyncLevel: SyncLevelDto.Word or SyncLevelDto.Line, Lines.Count: > 0 } && lyrics.Lines.All(IsTimed)
            ? new KaraokeLyrics([.. lyrics.Lines.Select(ToLine)])
            : null;

    private static bool IsTimed(LyricsLineDto line) =>
        line is { StartMs: not null, EndMs: not null } && line.Words.All(w => w is { StartMs: not null, EndMs: not null });

    private static KaraokeLine ToLine(LyricsLineDto line) =>
        new(line.StartMs!.Value, line.EndMs!.Value, [.. line.Words.Select(w => new KaraokeWord(w.Text, w.StartMs!.Value, w.EndMs!.Value))]);
}

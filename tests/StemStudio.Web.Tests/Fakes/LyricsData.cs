using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>Contract-shaped lyrics (backend-api 1.6.0 examples); every text is self-written placeholder text.</summary>
public static class LyricsData
{
    /// <summary>The word-synced <c>FoundLyrics</c> example: two lines, LRCLIB, word timing from the source.</summary>
    public static LyricsDto Found(SyncLevelDto sync = SyncLevelDto.Word, LyricsOriginDto origin = LyricsOriginDto.Source, int offsetMs = 0) => new()
    {
        Status = LyricsStatusDto.Found,
        SyncLevel = sync,
        Origin = origin,
        Language = "en",
        Source = LyricsSourceDto.Lrclib,
        Credit = origin == LyricsOriginDto.Aligned ? "Lyrics: LRCLIB; timing: automatic alignment" : "Lyrics: LRCLIB",
        OffsetMs = offsetMs,
        Lines =
        [
            Line(10950, 13200, ("Fake", 10950, 11400), ("line", 11400, 11900), ("one", 11900, 13200)),
            Line(14000, 16000, ("La", 14000, 14500), ("la", 14500, 15000), ("la", 15000, 16000)),
        ],
    };

    /// <summary>A plain document (no times) in the given status, e.g. alignPending with the CPU estimate.</summary>
    public static LyricsDto Plain(LyricsStatusDto status = LyricsStatusDto.AlignPending, string? reason = "Aligning takes about 7 minutes on the CPU.") => new()
    {
        Status = status,
        Reason = reason,
        SyncLevel = SyncLevelDto.Plain,
        Origin = LyricsOriginDto.None,
        Source = LyricsSourceDto.User,
        Credit = "Lyrics: entered by you",
        Lines = [PlainLine("Fake line one"), PlainLine("La la la")],
        Alignment = status == LyricsStatusDto.AlignPending ? new LyricsAlignmentDto(420, null, null) : null,
    };

    /// <summary>A running alignment.</summary>
    public static LyricsDto Aligning(int progress = 40, JobDeviceDto? device = null) =>
        Plain(LyricsStatusDto.Aligning, null) with { Alignment = new LyricsAlignmentDto(30, device ?? GpuDevice, progress) };

    /// <summary>A lyrics state without a document.</summary>
    public static LyricsDto Empty(LyricsStatusDto status, string? reason = null) => new() { Status = status, Reason = reason };

    /// <summary>The GPU device of an alignment.</summary>
    public static JobDeviceDto GpuDevice { get; } = new(ProcessingDeviceKindDto.Gpu, "NVIDIA GeForce RTX 4070", false, null);

    private static LyricsLineDto Line(int start, int end, params (string Text, int Start, int End)[] words) =>
        new(start, end, string.Join(' ', words.Select(w => w.Text)),
            [.. words.Select(w => new LyricsWordDto(w.Text, w.Start, w.End, WordOriginDto.Source, false))]);

    private static LyricsLineDto PlainLine(string text) =>
        new(null, null, text, [.. text.Split(' ').Select(w => new LyricsWordDto(w, null, null, null, false))]);
}

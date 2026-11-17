using StemStudio.Agent.LyricAssist;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Api.LyricAssist;

/// <summary>Host options of the lyric assistant (data-model 1; <c>StemStudio:LyricAssist</c>).</summary>
public sealed class LyricAssistHostOptions
{
    /// <summary>Configuration section.</summary>
    public const string SectionName = "StemStudio:LyricAssist";

    /// <summary>Lyric lines per model block (1-40, default 8).</summary>
    public int BlockLines { get; set; } = LyricAssistLimits.DefaultBlockLines;

    /// <summary>Polling interval of the event stream in milliseconds.</summary>
    public int StreamPollMilliseconds { get; set; } = 200;

    /// <summary>Seconds without any new event after which the stream closes (security review: idle timeout; the client reconnects with <c>after</c>).</summary>
    public int StreamIdleSeconds { get; set; } = 1800;

    /// <summary>Seconds between keep-alive comments of the event stream.</summary>
    public int KeepAliveSeconds { get; set; } = LyricAssistLimits.KeepAliveSeconds;
}

/// <summary>Builds the <see cref="LyricAssistJob"/> of an assist for the ai-agent assistant.</summary>
public static class LyricAssistJobs
{
    /// <summary>The job.</summary>
    /// <param name="assist">The assist.</param>
    /// <param name="sheet">The sheet.</param>
    /// <param name="blocks">Model blocks.</param>
    /// <param name="misses">Dictionary misses of the check.</param>
    /// <returns>The job.</returns>
    public static LyricAssistJob Build(AssistRun assist, LyricsSheet sheet, IReadOnlyList<SheetBlock> blocks, IReadOnlyList<WordMiss> misses)
    {
        ArgumentNullException.ThrowIfNull(assist);
        ArgumentNullException.ThrowIfNull(sheet);
        return new LyricAssistJob
        {
            AssistId = assist.Id,
            Kind = LyricAssistMapper.Map<AssistKindDto>(assist.Kind),
            Language = LyricAssistMapper.ToDto(assist.Language),
            Sensitivity = LyricAssistMapper.Map<SensitivityDto>(assist.Sensitivity),
            Title = sheet.Title,
            Text = sheet.Text,
            Blocks = blocks.Select(block => Block(block, misses)).ToList(),
            Categories = assist.Categories.Select(c => LyricAssistMapper.Map<SuggestionCategoryDto>(c)).ToList(),
            LineFrom = assist.LineFrom,
            LineTo = assist.LineTo,
            Count = assist.Count,
            Instruction = assist.Instruction,
            RhythmTarget = sheet.RhythmTarget is { } target ? LyricAssistMapper.ToDto(target) : null,
            IntentionalTexts = sheet.IntentionalMarks.Where(mark => IntentionalMarkMatcher.Occurs(mark, sheet.Text)).Select(mark => mark.Text).ToList(),
        };
    }

    private static LyricBlock Block(SheetBlock block, IReadOnlyList<WordMiss> misses) => new()
    {
        Index = block.Index,
        FirstLine = block.FirstLine,
        LastLine = block.LastLine,
        Misses = misses.Where(m => m.Line >= block.FirstLine && m.Line <= block.LastLine)
            .Select(m => new DictionaryMiss { Line = m.Line, Start = m.Start, Word = m.Word, Suggestions = m.Suggestions }).ToList(),
    };
}

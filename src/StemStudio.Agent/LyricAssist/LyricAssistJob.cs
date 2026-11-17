using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.LyricAssist;

/// <summary>One assist for the assistant (parameter object; built by the backend from the sheet and the request).</summary>
public sealed record LyricAssistJob
{
    /// <summary>Assist id (also the job id of <c>wait_for_job</c>).</summary>
    public required Guid AssistId { get; init; }

    /// <summary>What to do.</summary>
    public required AssistKindDto Kind { get; init; }

    /// <summary>Sheet language; explanations are written in it (owner decision Q3).</summary>
    public required AssistLanguageDto Language { get; init; }

    /// <summary>Sensitivity of the sheet (prompt hint; the backend filters).</summary>
    public required SensitivityDto Sensitivity { get; init; }

    /// <summary>Sheet title (context; the anchor text of title ideas).</summary>
    public required string Title { get; init; }

    /// <summary>Normalised sheet text (LF line ends), read-only context of every block.</summary>
    public required string Text { get; init; }

    /// <summary>Blocks of at most <see cref="LyricAssistLimits.DefaultBlockLines"/> lines, in order (Domain <c>SheetBlocks</c>).</summary>
    public required IReadOnlyList<LyricBlock> Blocks { get; init; }

    /// <summary>Requested categories (<c>check</c>, <c>devices</c>); empty = the defaults of the kind.</summary>
    public IReadOnlyList<SuggestionCategoryDto> Categories { get; init; } = [];

    /// <summary>Range start (1-based) or null.</summary>
    public int? LineFrom { get; init; }

    /// <summary>Range end (1-based) or null; <c>continue</c> inserts after it.</summary>
    public int? LineTo { get; init; }

    /// <summary>Requested count of the writing kinds, already defaulted by the backend.</summary>
    public int? Count { get; init; }

    /// <summary>Mood or theme hint (passed the imitation guard).</summary>
    public string? Instruction { get; init; }

    /// <summary>Rhythm target of the sheet, if set.</summary>
    public RhythmTargetDto? RhythmTarget { get; init; }

    /// <summary>Texts of the sheet's intentional marks that occur in the text (prompt hint: never flag them).</summary>
    public IReadOnlyList<string> IntentionalTexts { get; init; } = [];
}

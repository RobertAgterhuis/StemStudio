using System.Text.Json;
using System.Text.Json.Serialization;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.AgentEval.Lyric;

/// <summary>The lyric evaluation set (specs/023-lyric-writing-assistant/evaluation/lyric-eval-set.md 1.0.0 as data; keep both in sync).</summary>
/// <param name="Version">Version of the set.</param>
/// <param name="Cases">Scored sheets (N01-N32, E01-E32).</param>
/// <param name="Guardrails">Guardrail and injection cases (graded by the automated tests and the agent suite).</param>
/// <param name="Performance">Performance sheets P1/P2 (reference machine).</param>
internal sealed record LyricEvalSet(string Version, IReadOnlyList<LyricCase> Cases, IReadOnlyList<LyricGuardrail> Guardrails, IReadOnlyList<LyricPerformanceSheet> Performance)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    /// <summary>Reads the set.</summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The set.</returns>
    public static LyricEvalSet Parse(string json) => JsonSerializer.Deserialize<LyricEvalSet>(json, Json) ?? throw new InvalidDataException("The lyric evaluation set is empty.");
}

/// <summary>One sheet with its expected corrections and licence traps.</summary>
internal sealed record LyricCase
{
    public required string Id { get; init; }
    public required string Lang { get; init; }
    public required string Category { get; init; }
    public string Kind { get; init; } = "check";
    public required IReadOnlyList<string> Lines { get; init; }
    public IReadOnlyList<ExpectedCorrection> Expected { get; init; } = [];
    public IReadOnlyList<LicenceTrap> Forbidden { get; init; } = [];
    public IReadOnlyList<string> ExpectedTypes { get; init; } = [];
    public int? Line { get; init; }
    public int? Count { get; init; }
    public RhythmTargetDto? RhythmTarget { get; init; }
    public string? Note { get; init; }

    /// <summary>The sheet text (LF lines).</summary>
    public string Text => string.Join('\n', Lines);

    /// <summary>The assist kind.</summary>
    public AssistKindDto AssistKind => Kind switch
    {
        "devices" => AssistKindDto.Devices,
        "alternatives" => AssistKindDto.Alternatives,
        "continue" => AssistKindDto.Continue,
        "titles" => AssistKindDto.Titles,
        _ => AssistKindDto.Check,
    };

    /// <summary>The language.</summary>
    public AssistLanguageDto Language => Lang == "en" ? AssistLanguageDto.En : AssistLanguageDto.Nl;

    /// <summary>True for device and writing cases (graded deterministically and by the owner, SC-002).</summary>
    public bool IsCreative => AssistKind != AssistKindDto.Check;
}

/// <summary>A correction that must appear: the suggestion's original contains the anchor; the proposal is one of the forms.</summary>
internal sealed record ExpectedCorrection
{
    public string? Anchor { get; init; }
    public IReadOnlyList<string> Proposals { get; init; } = [];
    public IReadOnlyList<string> Types { get; init; } = [];
}

/// <summary>A span that must not get an <c>error</c> (or, with <see cref="Any"/>, any suggestion); no span = the whole sheet.</summary>
internal sealed record LicenceTrap
{
    public string? Span { get; init; }
    public int? Line { get; init; }
    public bool Any { get; init; }
}

/// <summary>A guardrail case (documentation; graded elsewhere).</summary>
internal sealed record LyricGuardrail(string Id, string Lang, string Case, string Expected);

/// <summary>A performance sheet.</summary>
internal sealed record LyricPerformanceSheet(string Id, string Lang, string Sheet, string Measured);

using System.Text.Json;
using System.Text.Json.Serialization;

namespace StemStudio.AgentEval;

/// <summary>The evaluation set (evaluation/eval-set.md 1.0.0 as data; keep both in sync).</summary>
/// <param name="Version">Version of the set.</param>
/// <param name="Cases">The cases.</param>
internal sealed record EvalSet(string Version, IReadOnlyList<EvalCase> Cases)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    /// <summary>Reads the set from JSON.</summary>
    public static EvalSet Parse(string json) => JsonSerializer.Deserialize<EvalSet>(json, Json) ?? throw new InvalidDataException("The evaluation set is empty.");
}

/// <summary>How the harness answers approval cards of a case.</summary>
internal enum ApprovalAnswer
{
    /// <summary>No card expected; any card is rejected.</summary>
    None,

    /// <summary>Approve every card.</summary>
    Approve,

    /// <summary>Reject every card.</summary>
    Reject,
}

/// <summary>One owner command with its grading rules.</summary>
internal sealed record EvalCase
{
    public required string Id { get; init; }

    public required string Lang { get; init; }

    public required string Page { get; init; }

    public string? TrackId { get; init; }

    public string? MixId { get; init; }
    public string? LyricsId { get; init; }

    public required string Command { get; init; }

    /// <summary>Alternative expectations; the case passes when one of them matches.</summary>
    public IReadOnlyList<Expectation> Expected { get; init; } = [];

    public IReadOnlyList<string> Forbidden { get; init; } = [];

    public bool ForbidNonRead { get; init; }

    public bool ForbidAnyCall { get; init; }

    public ApprovalAnswer Approval { get; init; }

    /// <summary>True when the harness sends an Undo request instead of the command (case 19).</summary>
    public bool Undo { get; init; }

    /// <summary>False for the supplementary guardrail cases S1-S3.</summary>
    public bool Scored { get; init; } = true;
}

/// <summary>A list of expected calls, in order when <see cref="Ordered"/>.</summary>
internal sealed record Expectation
{
    public required IReadOnlyList<ExpectedCall> Calls { get; init; }

    public bool Ordered { get; init; }
}

/// <summary>An expected call with argument matchers.</summary>
internal sealed record ExpectedCall
{
    public required string Tool { get; init; }

    public IReadOnlyDictionary<string, ArgumentMatcher> Args { get; init; } = new Dictionary<string, ArgumentMatcher>();
}

/// <summary>A matcher: <c>=</c> exact, <c>~</c> case-insensitive contains, <c>in</c> set, <c>&gt;</c>/<c>&lt;</c> numeric, <c>absent</c>.</summary>
internal sealed record ArgumentMatcher(string Op, JsonElement? Value = null);

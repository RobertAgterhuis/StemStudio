using System.Text.RegularExpressions;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Scores;

/// <summary>The facts of a ready score: the worker's summary plus where the model ran (data-model 4.1, column <c>Assets.Score</c>).</summary>
public sealed record ScoreFacts
{
    /// <summary>The profile.</summary>
    public required ScoreProfile Profile { get; init; }

    /// <summary>The engine that transcribed it.</summary>
    public required ScoreEngine Engine { get; init; }

    /// <summary>Piano only.</summary>
    public ScoreQuality? Quality { get; init; }

    /// <summary>The key; null for drums.</summary>
    public KeyInfo? Key { get; init; }

    /// <summary>Tempo mark (30..300).</summary>
    public required int Bpm { get; init; }

    /// <summary>Time signature, e.g. <c>4/4</c>.</summary>
    public required string TimeSignature { get; init; }

    /// <summary>Bars (&gt;= 1).</summary>
    public required int Bars { get; init; }

    /// <summary>Notes (&gt;= 1).</summary>
    public required int Notes { get; init; }

    /// <summary>PDF pages (&gt;= 1).</summary>
    public required int Pages { get; init; }

    /// <summary>The quantisation grid.</summary>
    public required ScoreResolution Resolution { get; init; }

    /// <summary>Warnings (at most 8).</summary>
    public IReadOnlyList<ScoreWarning> Warnings { get; init; } = [];

    /// <summary>Model credit (piano), or null.</summary>
    public string? Attribution { get; init; }

    /// <summary>Where the model ran, or null.</summary>
    public JobDevice? Device { get; init; }

    /// <summary>Tablature facts (feature 016), null without tablature.</summary>
    public TablatureFacts? Tablature { get; init; }

    /// <summary>Transposed part facts (feature 016), null for a concert score without a part.</summary>
    public PartFacts? Part { get; init; }

    /// <summary>Chord symbols were placed above the top staff (feature 016).</summary>
    public bool ChordsIncluded { get; init; }
}

/// <summary>Limits of <see cref="ScoreFacts"/> (backend contract 1.7.0 <c>ScoreFacts</c>), checked on worker data.</summary>
public static partial class ScoreFactsRules
{
    /// <summary>Most warnings.</summary>
    public const int MaxWarnings = 8;

    /// <summary>Longest warning or attribution text.</summary>
    public const int MaxTextLength = 300;

    private static readonly Bounds BpmBounds = new(30, 300);

    /// <summary>Validates the facts.</summary>
    /// <param name="facts">The facts.</param>
    /// <exception cref="DomainValidationException">A value breaks a rule.</exception>
    public static void Validate(ScoreFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var errors = new ValidationErrors();
        errors.Range("bpm", facts.Bpm, BpmBounds);
        errors.Check(TimeSignaturePattern().IsMatch(facts.TimeSignature ?? string.Empty), "timeSignature", "Must be n/4 with n 1..12.");
        CheckCounts(errors, facts);
        CheckKey(errors.Nested("key"), facts.Key);
        CheckTexts(errors, facts);
        errors.ThrowIfAny();
    }

    private static void CheckCounts(ValidationErrors errors, ScoreFacts facts)
    {
        errors.Check(facts.Bars >= 1, "bars", "Must be at least 1.");
        errors.Check(facts.Notes >= 1, "notes", "Must be at least 1.");
        errors.Check(facts.Pages >= 1, "pages", "Must be at least 1.");
    }

    private static void CheckKey(ValidationErrors errors, KeyInfo? key)
    {
        if (key is not null)
        {
            errors.Check(TonicPattern().IsMatch(key.Tonic ?? string.Empty), "tonic", "Must be A..G with an optional # or -.");
            errors.Range("confidence", key.Confidence, Bounds.Unit);
        }
    }

    private static void CheckTexts(ValidationErrors errors, ScoreFacts facts)
    {
        errors.Check(facts.Warnings.Count <= MaxWarnings, "warnings", $"Must be at most {MaxWarnings} warnings.");
        for (var index = 0; index < facts.Warnings.Count; index++)
        {
            var text = facts.Warnings[index].Text;
            errors.Check(!string.IsNullOrWhiteSpace(text) && text.Length <= MaxTextLength, $"warnings[{index}].text", $"Must be 1 to {MaxTextLength} characters.");
        }

        errors.Check((facts.Attribution?.Length ?? 0) <= MaxTextLength, "attribution", $"Must be at most {MaxTextLength} characters.");
    }

    [GeneratedRegex(@"\A([1-9]|1[0-2])/4\z", RegexOptions.CultureInvariant)]
    private static partial Regex TimeSignaturePattern();

    [GeneratedRegex(@"\A[A-G](#|-)?\z", RegexOptions.CultureInvariant)]
    private static partial Regex TonicPattern();
}

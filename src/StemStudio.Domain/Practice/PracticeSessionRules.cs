using System.Globalization;

namespace StemStudio.Domain.Practice;

/// <summary>Validation of a practice session upsert (014 FR-025, data-model 3.7).</summary>
public static class PracticeSessionRules
{
    /// <summary>Longest session.</summary>
    public static readonly TimeSpan MaxSpan = TimeSpan.FromHours(12);

    /// <summary>How far in the future a session may start (clock skew).</summary>
    public static readonly TimeSpan MaxFutureStart = TimeSpan.FromMinutes(5);

    /// <summary>Most section rows per session.</summary>
    public const int MaxSections = 64;

    /// <summary>Longest section label snapshot.</summary>
    public const int MaxLabelLength = 40;

    private const double PlayedSlackSeconds = 5;
    private const double SectionsSlackSeconds = 1;
    private static readonly Bounds Played = new(0, 43_200);
    private static readonly Bounds Speed = new(50, 120);

    /// <summary>Checks a session.</summary>
    /// <param name="input">The session.</param>
    /// <param name="nowUtc">Server time.</param>
    /// <returns>The violations (fields as in the request).</returns>
    public static IReadOnlyList<DomainError> Validate(PracticeSessionInput input, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(input);
        var errors = new ValidationErrors();
        ValidateTimes(errors, input, nowUtc);
        errors.Check(input.LoopPasses >= 0, "loopPasses", "Loop passes must not be negative.");
        errors.Check(input.MaxSpeedPercent is null || Speed.Contains(input.MaxSpeedPercent.Value), "maxSpeedPercent", Speed.Message);
        ValidateSections(errors, input);
        return errors.Errors;
    }

    private static void ValidateTimes(ValidationErrors errors, PracticeSessionInput input, DateTime nowUtc)
    {
        var span = input.EndedAt - input.StartedAt;
        errors.Check(input.StartedAt <= nowUtc + MaxFutureStart, "startedAt", "The session must not start in the future.");
        errors.Check(span >= TimeSpan.Zero, "endedAt", "The session must not end before it starts.");
        errors.Check(span <= MaxSpan, "endedAt", "A session can last at most 12 hours.");
        errors.Range("playedSeconds", input.PlayedSeconds, Played);
        var withinSpan = input.PlayedSeconds <= Math.Max(0, span.TotalSeconds) + PlayedSlackSeconds;
        errors.Check(withinSpan, "playedSeconds", "The played time must not be longer than the session.");
    }

    private static void ValidateSections(ValidationErrors errors, PracticeSessionInput input)
    {
        var sections = input.Sections ?? [];
        errors.Check(sections.Count <= MaxSections, "sections", $"At most {MaxSections} section rows are allowed.");
        errors.Check(sections.Sum(section => section.PlayedSeconds) <= input.PlayedSeconds + SectionsSlackSeconds, "sections", "The section times must not add up to more than the played time.");
        for (var i = 0; i < sections.Count; i++)
        {
            ValidateSection(errors.Nested(string.Create(CultureInfo.InvariantCulture, $"sections[{i}]")), sections[i]);
        }
    }

    private static void ValidateSection(ValidationErrors errors, PracticeSectionInput section)
    {
        errors.Range("playedSeconds", section.PlayedSeconds, Played);
        errors.Check(Clean(section.Label).Length <= MaxLabelLength, "label", $"The label may have at most {MaxLabelLength} characters.");
        errors.Check(section.LoopPasses >= 0, "loopPasses", "Loop passes must not be negative.");
        errors.Check(section.TrainerReachedPercent is null || Speed.Contains(section.TrainerReachedPercent.Value), "trainerReachedPercent", Speed.Message);
    }

    /// <summary>A trimmed label (empty for null).</summary>
    /// <param name="label">Raw label.</param>
    /// <returns>The label.</returns>
    internal static string Clean(string? label) => label?.Trim() ?? string.Empty;
}

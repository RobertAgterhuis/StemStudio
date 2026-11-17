using System.Globalization;
using System.Text;
using StemStudio.Agent.LyricAssist;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.AgentEval.Lyric;

/// <summary>The grade of one case run (lyric-eval-set.md "Grading").</summary>
/// <param name="Case">The case.</param>
/// <param name="Found">Expected corrections found.</param>
/// <param name="Expected">Expected corrections.</param>
/// <param name="Errors"><c>error</c> suggestions.</param>
/// <param name="MatchedErrors"><c>error</c> suggestions that match an expected correction.</param>
/// <param name="Traps">Licence trap spans.</param>
/// <param name="TrapHits">Trap spans that got an error (or any suggestion for "none on").</param>
/// <param name="CreativePassed">Device and writing cases: enough valid suggestions of the expected type; null otherwise.</param>
/// <param name="Seconds">Latency of the assist.</param>
internal sealed record LyricGrade(LyricCase Case, int Found, int Expected, int Errors, int MatchedErrors, int Traps, int TrapHits, bool? CreativePassed, double Seconds);

/// <summary>Grades the validated suggestions of a case (pure) and summarises a run per language and category.</summary>
internal static class LyricGrader
{
    /// <summary>Grades one case.</summary>
    /// <param name="evalCase">The case.</param>
    /// <param name="suggestions">The validated suggestions of every block.</param>
    /// <param name="seconds">Latency.</param>
    /// <returns>The grade.</returns>
    public static LyricGrade Grade(LyricCase evalCase, IReadOnlyList<AssistantSuggestion> suggestions, double seconds)
    {
        var errors = suggestions.Where(suggestion => suggestion.Severity == SuggestionSeverityDto.Error).ToList();
        var found = evalCase.Expected.Count(expected => suggestions.Any(suggestion => Matches(expected, suggestion)));
        var matchedErrors = errors.Count(error => evalCase.Expected.Any(expected => Matches(expected, error)));
        var trapHits = evalCase.Forbidden.Count(trap => suggestions.Any(suggestion => Hits(evalCase, trap, suggestion)));
        bool? creative = evalCase.IsCreative
            ? suggestions.Count(suggestion => evalCase.ExpectedTypes.Contains(LyricTypes.Name(suggestion.Type))) >= Math.Max(1, evalCase.AssistKind == AssistKindDto.Devices || evalCase.AssistKind == AssistKindDto.Continue ? 1 : evalCase.Count ?? 1)
            : null;
        return new LyricGrade(evalCase, found, evalCase.Expected.Count, errors.Count, matchedErrors, evalCase.Forbidden.Count, trapHits, creative, seconds);
    }

    /// <summary>The report lines: recall, precision and licence false-positive rate per language and category, creative cases, latency.</summary>
    /// <param name="title">Run title.</param>
    /// <param name="grades">Grades of every case and run.</param>
    /// <returns>Markdown.</returns>
    public static string Report(string title, IReadOnlyList<LyricGrade> grades)
    {
        var text = new StringBuilder().AppendLine(CultureInfo.InvariantCulture, $"# Lyric suite: {title}").AppendLine()
            .AppendLine("| Scope | Recall | Precision | Licence FP | Creative |").AppendLine("|---|---|---|---|---|");
        foreach (var group in grades.GroupBy(grade => grade.Case.Lang).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            text.AppendLine(Row(group.Key, [.. group]));
            foreach (var category in group.GroupBy(grade => grade.Case.Category).OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                text.AppendLine(Row($"{group.Key} {category.Key}", [.. category]));
            }
        }

        var latencies = grades.Select(grade => grade.Seconds).Order().ToList();
        return text.AppendLine().AppendLine(CultureInfo.InvariantCulture, $"Latency per case: p50 {Percentile(latencies, 0.5):0.0} s, p90 {Percentile(latencies, 0.9):0.0} s.").ToString();
    }

    /// <summary>Recall over all expected corrections of the grades (1 when there are none).</summary>
    /// <param name="grades">Grades.</param>
    /// <returns>Recall 0..1.</returns>
    public static double Recall(IReadOnlyList<LyricGrade> grades) => Ratio(grades.Sum(grade => grade.Found), grades.Sum(grade => grade.Expected), 1);

    /// <summary>Precision of the errors (1 when there are none).</summary>
    /// <param name="grades">Grades.</param>
    /// <returns>Precision 0..1.</returns>
    public static double Precision(IReadOnlyList<LyricGrade> grades) => Ratio(grades.Sum(grade => grade.MatchedErrors), grades.Sum(grade => grade.Errors), 1);

    /// <summary>Licence false-positive rate (0 when there are no traps).</summary>
    /// <param name="grades">Grades.</param>
    /// <returns>Rate 0..1.</returns>
    public static double LicenceFalsePositives(IReadOnlyList<LyricGrade> grades) => Ratio(grades.Sum(grade => grade.TrapHits), grades.Sum(grade => grade.Traps), 0);

    private static string Row(string scope, IReadOnlyList<LyricGrade> grades)
    {
        var creative = grades.Where(grade => grade.CreativePassed is not null).ToList();
        return string.Create(CultureInfo.InvariantCulture,
            $"| {scope} | {Recall(grades):P0} | {Precision(grades):P0} | {LicenceFalsePositives(grades):P0} | {creative.Count(grade => grade.CreativePassed == true)}/{creative.Count} |");
    }

    private static bool Matches(ExpectedCorrection expected, AssistantSuggestion suggestion) =>
        (expected.Anchor is null || suggestion.Anchor.Original.Contains(expected.Anchor, StringComparison.OrdinalIgnoreCase))
        && (expected.Proposals.Count == 0 || expected.Proposals.Any(form => (suggestion.Proposal ?? string.Empty).Contains(form, StringComparison.OrdinalIgnoreCase)))
        && (expected.Types.Count == 0 || expected.Types.Contains(LyricTypes.Name(suggestion.Type)));

    private static bool Hits(LyricCase evalCase, LicenceTrap trap, AssistantSuggestion suggestion)
    {
        if (!trap.Any && suggestion.Severity != SuggestionSeverityDto.Error)
        {
            return false;
        }

        return trap switch
        {
            { Span: { } span } => Overlaps(evalCase, span, suggestion.Anchor),
            { Line: { } line } => suggestion.Anchor.Line == line,
            _ => true,
        };
    }

    /// <summary>The anchor overlaps an occurrence of the span on its line (case-insensitive).</summary>
    private static bool Overlaps(LyricCase evalCase, string span, AnchorDto anchor)
    {
        var line = anchor.Line >= 1 && anchor.Line <= evalCase.Lines.Count ? evalCase.Lines[anchor.Line - 1] : string.Empty;
        for (var start = line.IndexOf(span, StringComparison.OrdinalIgnoreCase); start >= 0; start = line.IndexOf(span, start + 1, StringComparison.OrdinalIgnoreCase))
        {
            if (anchor.Start < start + span.Length && start < anchor.Start + Math.Max(1, anchor.Length))
            {
                return true;
            }
        }

        return false;
    }

    private static double Ratio(int part, int whole, double empty) => whole == 0 ? empty : (double)part / whole;

    private static double Percentile(List<double> sorted, double p) => sorted.Count == 0 ? 0 : sorted[(int)Math.Min(sorted.Count - 1, Math.Floor(p * sorted.Count))];
}

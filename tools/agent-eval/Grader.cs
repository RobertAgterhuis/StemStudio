using System.Globalization;
using System.Text.Json;

namespace StemStudio.AgentEval;

/// <summary>A tool call the run made (from its steps).</summary>
/// <param name="Tool">Tool name.</param>
/// <param name="Arguments">Canonical JSON arguments.</param>
/// <param name="Executed">True when the step succeeded.</param>
/// <param name="Read">True for read/meta tools.</param>
internal sealed record RecordedCall(string Tool, string Arguments, bool Executed, bool Read);

/// <summary>The grade of one case run.</summary>
/// <param name="Passed">Pass or fail.</param>
/// <param name="Reason">Why it failed, or null.</param>
internal sealed record Grade(bool Passed, string? Reason);

/// <summary>The grading rules of evaluation/eval-set.md: expected calls, forbidden calls, at most 2 extra reads.</summary>
internal static class Grader
{
    private const int MaxExtraReads = 2;
    private static readonly HashSet<string> Uncounted = ["set_plan", "wait_for_job"];

    public static Grade Grade(EvalCase evalCase, IReadOnlyList<RecordedCall> calls)
    {
        var counted = calls.Where(call => !Uncounted.Contains(call.Tool)).ToList();
        return Forbidden(evalCase, counted) is { } forbidden
            ? new Grade(false, forbidden)
            : Expected(evalCase, counted);
    }

    private static string? Forbidden(EvalCase evalCase, List<RecordedCall> calls)
    {
        if (evalCase.ForbidAnyCall && calls.Count > 0)
        {
            return "no tool call was expected";
        }

        var executed = calls.Where(call => call.Executed).ToList();
        if (evalCase.ForbidNonRead && executed.Any(call => !call.Read))
        {
            return "a non-read call executed";
        }

        return executed.FirstOrDefault(call => evalCase.Forbidden.Contains(call.Tool)) is { } hit ? $"forbidden call {hit.Tool} executed" : null;
    }

    private static Grade Expected(EvalCase evalCase, List<RecordedCall> calls)
    {
        if (evalCase.Expected.Count == 0)
        {
            return new Grade(true, null);
        }

        var match = evalCase.Expected.Select(expectation => Matches(expectation, calls)).FirstOrDefault(used => used is not null);
        if (match is null)
        {
            return new Grade(false, "expected calls missing or with other arguments");
        }

        var extraReads = calls.Count(call => call.Read && !match.Contains(call));
        return extraReads > MaxExtraReads ? new Grade(false, $"{extraReads} extra read calls") : new Grade(true, null);
    }

    /// <summary>The calls that satisfy the expectation, or null.</summary>
    private static HashSet<RecordedCall>? Matches(Expectation expectation, List<RecordedCall> calls)
    {
        var used = new HashSet<RecordedCall>(ReferenceEqualityComparer.Instance);
        var from = 0;
        foreach (var expected in expectation.Calls)
        {
            var index = calls.FindIndex(expectation.Ordered ? from : 0, call => !used.Contains(call) && Matches(expected, call));
            if (index < 0)
            {
                return null;
            }

            used.Add(calls[index]);
            from = index + 1;
        }

        return used;
    }

    private static bool Matches(ExpectedCall expected, RecordedCall call)
    {
        if (expected.Tool != call.Tool)
        {
            return false;
        }

        using var document = JsonDocument.Parse(call.Arguments);
        return expected.Args.All(pair => Matches(pair.Value, document.RootElement.TryGetProperty(pair.Key, out var value) ? value : null));
    }

    internal static bool Matches(ArgumentMatcher matcher, JsonElement? actual) => matcher.Op switch
    {
        "absent" => actual is null,
        "=" => actual is { } value && matcher.Value is { } wanted && Equal(value, wanted),
        "~" => actual is { ValueKind: JsonValueKind.String } text && text.GetString()!.Contains(matcher.Value!.Value.GetString()!, StringComparison.OrdinalIgnoreCase),
        "in" => actual is { ValueKind: JsonValueKind.Array } list && SameSet(list, matcher.Value!.Value),
        ">" => actual is { ValueKind: JsonValueKind.Number } more && more.GetDouble() > matcher.Value!.Value.GetDouble(),
        "<" => actual is { ValueKind: JsonValueKind.Number } less && less.GetDouble() < matcher.Value!.Value.GetDouble(),
        _ => false,
    };

    private static bool Equal(JsonElement actual, JsonElement wanted) => actual.ValueKind == JsonValueKind.Number && wanted.ValueKind == JsonValueKind.Number
        ? Math.Abs(actual.GetDouble() - wanted.GetDouble()) < 1e-9
        : string.Equals(Text(actual), Text(wanted), StringComparison.Ordinal);

    private static bool SameSet(JsonElement actual, JsonElement wanted) =>
        actual.EnumerateArray().Select(Text).Order(StringComparer.Ordinal).SequenceEqual(wanted.EnumerateArray().Select(Text).Order(StringComparer.Ordinal));

    private static string Text(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
}

/// <summary>The Markdown report: success per case and language, latency p50/p90.</summary>
internal static class EvalReport
{
    public static string Render(string backend, IReadOnlyList<(EvalCase Case, Grade Grade, TimeSpan Latency)> results)
    {
        var lines = new List<string> { $"# Agent evaluation ({backend})", string.Empty, Summary(results), string.Empty, "| Case | Lang | Pass | Reason | Seconds |", "|---|---|---|---|---|" };
        lines.AddRange(results.Select(result => $"| {result.Case.Id} | {result.Case.Lang} | {(result.Grade.Passed ? "yes" : "no")} | {result.Grade.Reason ?? string.Empty} | {result.Latency.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} |"));
        return string.Join('\n', lines) + "\n";
    }

    private static string Summary(IReadOnlyList<(EvalCase Case, Grade Grade, TimeSpan Latency)> results)
    {
        var scored = results.Where(result => result.Case.Scored).ToList();
        var latencies = results.Select(result => result.Latency.TotalSeconds).Order().ToList();
        return $"Scored success: {Rate(scored)} (nl {Rate([.. scored.Where(result => result.Case.Lang == "nl")])}, en {Rate([.. scored.Where(result => result.Case.Lang == "en")])}); " +
            $"supplementary: {Rate([.. results.Where(result => !result.Case.Scored)])}; latency p50 {Percentile(latencies, 0.5)} s, p90 {Percentile(latencies, 0.9)} s.";
    }

    private static string Rate(List<(EvalCase Case, Grade Grade, TimeSpan Latency)> results) =>
        results.Count == 0 ? "n/a" : $"{results.Count(result => result.Grade.Passed)}/{results.Count}";

    internal static string Percentile(List<double> sorted, double p) =>
        sorted.Count == 0 ? "n/a" : sorted[(int)Math.Ceiling(p * sorted.Count) - 1].ToString("0.0", CultureInfo.InvariantCulture);
}

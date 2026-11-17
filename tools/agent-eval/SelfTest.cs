using System.Text.Json;
using System.Text.Json.Nodes;

namespace StemStudio.AgentEval;

/// <summary>
/// The grading self-test (<c>--backend fake</c>, no model): for every case a scripted ideal transcript must pass and scripted
/// faulty transcripts (forbidden call executed, a missing expected call, too many extra reads) must fail.
/// </summary>
internal static class SelfTest
{
    private static readonly RecordedCall ExtraRead = new("get_track", "{}", true, true);

    public static IReadOnlyList<string> Run(EvalSet set)
    {
        var failures = new List<string>();
        foreach (var evalCase in set.Cases)
        {
            CheckCase(failures, evalCase);
        }

        return failures;
    }

    private static void CheckCase(List<string> failures, EvalCase evalCase)
    {
        Check(failures, evalCase, ("ideal", Ideal(evalCase)), expectPass: true);
        Check(failures, evalCase, ("3 extra reads", [.. Ideal(evalCase), ExtraRead, ExtraRead, ExtraRead]), expectPass: evalCase.Expected.Count == 0 && !evalCase.ForbidAnyCall);
        if (evalCase.Forbidden.Count > 0)
        {
            Check(failures, evalCase, ("forbidden executed", [.. Ideal(evalCase), new RecordedCall(evalCase.Forbidden[0], "{}", true, false)]), expectPass: false);
        }

        if (evalCase.Expected.Count > 0)
        {
            Check(failures, evalCase, ("nothing called", []), expectPass: false);
        }
    }

    private static void Check(List<string> failures, EvalCase evalCase, (string Name, IReadOnlyList<RecordedCall> Calls) scenario, bool expectPass)
    {
        var grade = Grader.Grade(evalCase, scenario.Calls);
        if (grade.Passed != expectPass)
        {
            failures.Add($"case {evalCase.Id} ({scenario.Name}): expected {(expectPass ? "pass" : "fail")}, got {(grade.Passed ? "pass" : "fail")} {grade.Reason}");
        }
    }

    /// <summary>The calls of the first expectation with arguments that satisfy their matchers.</summary>
    internal static IReadOnlyList<RecordedCall> Ideal(EvalCase evalCase) => evalCase.Expected.Count == 0
        ? []
        : [.. evalCase.Expected[0].Calls.Select(call => new RecordedCall(call.Tool, Arguments(call), true, IsRead(call.Tool)))];

    private static readonly string[] ReadPrefixes = ["list_", "search_", "get_", "find_", "browse_", "count_"];

    /// <summary>True for the read tools of the catalogue (by their verb).</summary>
    private static bool IsRead(string tool) => ReadPrefixes.Any(prefix => tool.StartsWith(prefix, StringComparison.Ordinal));

    private static string Arguments(ExpectedCall call)
    {
        var arguments = new JsonObject();
        foreach (var (name, matcher) in call.Args.Where(pair => pair.Value.Op != "absent"))
        {
            arguments[name] = Satisfying(matcher);
        }

        return arguments.ToJsonString();
    }

    private static JsonNode? Satisfying(ArgumentMatcher matcher) => matcher.Op switch
    {
        ">" => JsonValue.Create(matcher.Value!.Value.GetDouble() + 1),
        "<" => JsonValue.Create(matcher.Value!.Value.GetDouble() - 1),
        "has" => new JsonArray(JsonNode.Parse(matcher.Value!.Value.GetRawText())),
        _ => JsonNode.Parse(matcher.Value!.Value.GetRawText()),
    };

    /// <summary>Loads and checks the set.</summary>
    public static int Execute(string path)
    {
        var set = EvalSet.Parse(File.ReadAllText(path));
        var failures = Run(set);
        foreach (var failure in failures)
        {
            Console.Error.WriteLine(failure);
        }

        Console.WriteLine($"agent-eval self-test: {set.Cases.Count} cases, {failures.Count} grading failures.");
        return failures.Count == 0 ? 0 : 1;
    }

    /// <summary>Serialises a recorded transcript for the report appendix.</summary>
    public static string Describe(IReadOnlyList<RecordedCall> calls) => JsonSerializer.Serialize(calls.Select(call => call.Tool));
}

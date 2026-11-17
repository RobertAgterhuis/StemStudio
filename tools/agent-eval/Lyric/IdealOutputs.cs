using System.Globalization;
using System.Text.Json.Nodes;
using StemStudio.Agent.LyricAssist;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.AgentEval.Lyric;

/// <summary>Scripted outputs of the lyric self-test: the ideal answer of a case, and a hostile answer that flags every trap.</summary>
internal static class IdealOutputs
{
    /// <summary>The ideal lyric-assist-output of a case.</summary>
    /// <param name="evalCase">The case.</param>
    /// <returns>The output object.</returns>
    public static JsonObject For(LyricCase evalCase) => new()
    {
        ["items"] = new JsonArray([.. evalCase.IsCreative ? Creative(evalCase) : evalCase.Expected.Select(expected => Correction(evalCase, expected))]),
    };

    /// <summary>An output with an error on every trap span (or the first reviewable line).</summary>
    /// <param name="evalCase">The case.</param>
    /// <returns>The output object.</returns>
    public static JsonObject Traps(LyricCase evalCase) => new()
    {
        ["items"] = new JsonArray([.. evalCase.Forbidden.Select(trap => Trap(evalCase, trap)).OfType<JsonNode>()]),
    };

    private static JsonObject Correction(LyricCase evalCase, ExpectedCorrection expected)
    {
        var line = LineOf(evalCase, expected.Anchor);
        var original = expected.Anchor ?? evalCase.Lines[line - 1];
        var proposal = expected.Proposals.Count > 0 ? expected.Proposals[0] : Shorter(original);
        return Item(expected.Types.Count > 0 ? expected.Types[0] : DefaultType(evalCase.Category), "error", line, original, proposal);
    }

    private static JsonObject? Trap(LyricCase evalCase, LicenceTrap trap)
    {
        var line = trap.Line ?? LineOf(evalCase, trap.Span);
        var text = evalCase.Lines[line - 1];
        var original = trap.Span is { } span && text.Contains(span, StringComparison.OrdinalIgnoreCase) ? text.Substring(text.IndexOf(span, StringComparison.OrdinalIgnoreCase), span.Length) : text.Split(' ')[0];
        return LyricText.IsMarker(text) ? null : Item("grammar.other", "error", line, original, original + "x");
    }

    private static IEnumerable<JsonNode> Creative(LyricCase evalCase) => evalCase.AssistKind switch
    {
        AssistKindDto.Alternatives => Enumerable.Range(1, evalCase.Count ?? 3).Select(index => Item("write.alternative", "idea", evalCase.Line!.Value, evalCase.Lines[evalCase.Line.Value - 1], $"{evalCase.Lines[evalCase.Line.Value - 1]} ({index})")),
        AssistKindDto.Continue => [Item("write.continuation", "idea", evalCase.Line!.Value, string.Empty, string.Join('\n', Enumerable.Range(1, evalCase.Count ?? 2).Select(index => $"Nieuwe regel {index}")))],
        AssistKindDto.Titles => Enumerable.Range(1, evalCase.Count ?? 5).Select(index => Item("write.title", "idea", 0, LyricSuite.Title(evalCase), $"Title {index}")),
        _ => [(JsonNode)Device(evalCase)],
    };

    /// <summary>Devices: a syllable fit that shortens the line, or an end rhyme on the case line with line 1's last word.</summary>
    private static JsonObject Device(LyricCase evalCase)
    {
        var line = evalCase.Line ?? 1;
        var text = evalCase.Lines[line - 1];
        if (evalCase.ExpectedTypes.Contains("device.syllable-fit"))
        {
            var words = text.Split(' ');
            return Item("device.syllable-fit", "idea", line, string.Join(' ', words[^3..]), words[^1]);
        }

        var partner = LyricText.LastWord(evalCase.Lines[0])!;
        var item = Item("device.end-rhyme", "idea", line, text, text + " x" + partner);
        item["rhymeWithLine"] = 1;
        item["rhymeWord"] = partner;
        return item;
    }

    private static int LineOf(LyricCase evalCase, string? text)
    {
        for (var index = 0; index < evalCase.Lines.Count; index++)
        {
            if (!LyricText.IsMarker(evalCase.Lines[index]) && (text is null || evalCase.Lines[index].Contains(text, StringComparison.OrdinalIgnoreCase)))
            {
                return index + 1;
            }
        }

        return 1;
    }

    private static string Shorter(string original) => original.Contains(' ', StringComparison.Ordinal) ? original[..original.LastIndexOf(' ')] : original + "!";

    private static string DefaultType(string category) => category switch
    {
        "spelling" => "spelling.typo",
        "grammar" => "grammar.other",
        _ => "flow.awkward",
    };

    private static JsonObject Item(string type, string severity, int line, string original, string proposal) => new()
    {
        ["type"] = type,
        ["severity"] = severity,
        ["line"] = line,
        ["original"] = original,
        ["proposal"] = proposal,
        ["explanation"] = string.Create(CultureInfo.InvariantCulture, $"Ideal {type}."),
    };
}

/// <summary>
/// The deterministic checks of the harness (an approximation of the API's Domain services, which the harness cannot reference):
/// vowel groups as syllables, a shared last vowel group and coda as a rhyme, no verbatim library (the harness has no lyrics).
/// </summary>
internal sealed class HarnessVerification : ILyricVerification
{
    private const string Vowels = "aeiouyàáâäèéêëìíîïòóôöùúûü";

    public int CountSyllables(string line, AssistLanguageDto language)
    {
        if (LyricText.IsMarker(line))
        {
            return 0;
        }

        var count = 0;
        var inVowel = false;
        foreach (var c in line.ToLowerInvariant())
        {
            var vowel = Vowels.Contains(c, StringComparison.Ordinal);
            count += vowel && !inVowel ? 1 : 0;
            inVowel = vowel;
        }

        return count;
    }

    public RhymeKindDto? Rhyme(string word, string other, AssistLanguageDto language) =>
        Tail(word) is { Length: > 0 } tail && tail == Tail(other) ? RhymeKindDto.Perfect : null;

    public bool IsVerbatim(string generatedText) => false;

    /// <summary>The last vowel group with what follows it.</summary>
    private static string Tail(string word)
    {
        var lower = word.ToLowerInvariant();
        var end = lower.Length - 1;
        while (end >= 0 && !Vowels.Contains(lower[end], StringComparison.Ordinal))
        {
            end--;
        }

        var start = end;
        while (start > 0 && Vowels.Contains(lower[start - 1], StringComparison.Ordinal))
        {
            start--;
        }

        return end < 0 ? string.Empty : lower[start..];
    }
}

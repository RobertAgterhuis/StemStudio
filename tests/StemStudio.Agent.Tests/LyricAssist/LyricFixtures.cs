using StemStudio.Agent.LyricAssist;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.Tests.LyricAssist;

/// <summary>Deterministic <see cref="ILyricVerification"/> for tests: vowel groups as syllables, shared last two letters as a rhyme.</summary>
public sealed class FakeVerification : ILyricVerification
{
    /// <summary>Texts that count as library lyrics (verbatim).</summary>
    public List<string> Library { get; } = [];

    /// <summary>Number of syllable counts asked for.</summary>
    public int Calls { get; private set; }

    /// <inheritdoc />
    public int CountSyllables(string line, AssistLanguageDto language)
    {
        Calls++;
        if (LyricText.IsMarker(line))
        {
            return 0;
        }

        var count = 0;
        var inVowel = false;
        foreach (var c in line.ToLowerInvariant())
        {
            var vowel = "aeiouy".Contains(c, StringComparison.Ordinal);
            count += vowel && !inVowel ? 1 : 0;
            inVowel = vowel;
        }

        return count;
    }

    /// <inheritdoc />
    public RhymeKindDto? Rhyme(string word, string other, AssistLanguageDto language) =>
        word.Length >= 2 && other.Length >= 2 && word[^2..].Equals(other[^2..], StringComparison.OrdinalIgnoreCase) ? RhymeKindDto.Perfect : null;

    /// <inheritdoc />
    public bool IsVerbatim(string generatedText) => Library.Any(text => generatedText.Contains(text, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Sample jobs over the "Zomeravond" fixture (tests/fixtures/lyrics/assist/sheets/zomeravond.nl.txt, CC0).</summary>
public static class LyricFixtures
{
    public const string Zomeravond = """
        [verse]
        De zomeravond zakt langzaam weg over de oude dijk
        de wind fluisterd zacht door het hoge riet
        'k Heb je lief, zo stil als 't licht
        dat valt op jouw wangen

        [chorus]
        Gij zijt de ster die mij de weg wijst
        Naar huis ik ga, de straat is leeg
        de stad word stil als de klok twaalf slaat
        en het is zo dat ik eigenlijk wel heel erg moe ben

        [verse]
        Ik zal deze nacht nooit vergeeten
        Zelmara, de lampen gaan een voor een uit
        """;

    public static string Text { get; } = Zomeravond.Replace("\r\n", "\n", StringComparison.Ordinal);

    public static LyricBlock Block0 { get; } = new()
    {
        Index = 0,
        FirstLine = 1,
        LastLine = 6,
        Misses = [new DictionaryMiss { Line = 3, Start = 8, Word = "fluisterd", Suggestions = ["fluistert", "fluisteren"] }],
    };

    public static LyricBlock Block1 { get; } = new() { Index = 1, FirstLine = 7, LastLine = 12 };

    public static LyricBlock Block2 { get; } = new()
    {
        Index = 2,
        FirstLine = 13,
        LastLine = 15,
        Misses = [new DictionaryMiss { Line = 14, Start = 18, Word = "vergeeten" }, new DictionaryMiss { Line = 15, Start = 0, Word = "Zelmara" }],
    };

    public static LyricAssistJob Job(AssistKindDto kind = AssistKindDto.Check) => new()
    {
        AssistId = Guid.Parse("0e0e0e0e-0000-0000-0000-000000000001"),
        Kind = kind,
        Language = AssistLanguageDto.Nl,
        Sensitivity = SensitivityDto.Balanced,
        Title = "Zomeravond",
        Text = Text,
        Blocks = [Block0, Block1, Block2],
    };

    public static ValidationContext Context(LyricAssistJob job, LyricBlock block, FakeVerification? verification = null) =>
        new(job, block, verification ?? new FakeVerification(), Guid.Parse("99999999-0000-0000-0000-000000000009"));

    public static ParsedLyricOutput Output(params ModelSuggestionItem[] items) => new(items, [], 0);

    public static ModelSuggestionItem Item(SuggestionTypeDto type, int line, string original, string? proposal, SuggestionSeverityDto severity = SuggestionSeverityDto.Error) =>
        new() { Type = type, Severity = severity, Line = line, Original = original, Proposal = proposal, Explanation = "Uitleg." };
}

using System.Text.Json;
using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Domain.Tests.Lyrics.Assist;

/// <summary>023 T038: syllable counters against the gold lists (SC-007), rhyme keys against the vectors, rhythm target and medians (FR-014, FR-017).</summary>
public sealed class PhonologyTests
{
    private static readonly Lazy<FileLexicon> Cmu = new(() => new FileLexicon(Path.Combine(Path.GetDirectoryName(AssistFixtures.Root)!, "..", "..", "..", "src", "StemStudio.Api", "LyricAssist", "Dictionaries", "cmudict", "cmudict.dict")));

    private static LyricPhonology Phonology => new(Cmu.Value);

    [Fact]
    public void DutchCounter_MeetsTheGoldList()
    {
        var gold = Gold("syllables-nl.tsv");
        var wrong = gold.Where(g => DutchPhonology.Syllables(g.Word) != g.Count).Select(g => $"{g.Word}:{g.Count}/{DutchPhonology.Syllables(g.Word)}").ToList();

        (1.0 - ((double)wrong.Count / gold.Count)).ShouldBeGreaterThanOrEqualTo(0.97, string.Join(' ', wrong));
    }

    [Fact]
    public void EnglishCounter_MeetsTheGoldList()
    {
        var gold = Gold("syllables-en.tsv");
        var english = new EnglishPhonology(Cmu.Value);
        var wrong = gold.Where(g => english.Syllables(g.Word) != g.Count && !(g.Note == "ambiguous" && Math.Abs(english.Syllables(g.Word) - g.Count) == 1))
            .Select(g => $"{g.Word}:{g.Count}/{english.Syllables(g.Word)}").ToList();

        (1.0 - ((double)wrong.Count / gold.Count)).ShouldBeGreaterThanOrEqualTo(0.95, string.Join(' ', wrong));
    }

    [Theory]
    [InlineData("rhymes-nl.json", AssistLanguage.Dutch)]
    [InlineData("rhymes-en.json", AssistLanguage.English)]
    public void RhymeKeys_MatchTheVectors(string file, AssistLanguage language)
    {
        var pairs = JsonDocument.Parse(AssistFixtures.Read(file)).RootElement.GetProperty("pairs").EnumerateArray()
            .Select(p => (A: p.GetProperty("a").GetString()!, B: p.GetProperty("b").GetString()!, Expected: p.GetProperty("expected").GetString()!)).ToList();

        var wrong = pairs.Where(p => Wire(Phonology.Rhyme(p.A, p.B, language)) != p.Expected).Select(p => $"{p.A}/{p.B}={p.Expected}:{Wire(Phonology.Rhyme(p.A, p.B, language))}").ToList();

        wrong.ShouldBeEmpty();
    }

    [Fact]
    public void Rhyme_NeverWithItselfOrWithoutVowel()
    {
        Phonology.Rhyme("Nacht", "nacht", AssistLanguage.Dutch).ShouldBeNull();
        Phonology.Rhyme("'k", "nacht", AssistLanguage.Dutch).ShouldBeNull();
        Phonology.RhymeKey("xyzzyq", AssistLanguage.English).ShouldBe(("y", "q"));
        Phonology.RhymeKey("hmm", AssistLanguage.English).ShouldBe((string.Empty, string.Empty));
        new EnglishPhonology(Cmu.Value).Syllables(" ").ShouldBe(0);
    }

    [Fact]
    public void LineSyllables_SkipMarkersBlankLinesAndNumbers()
    {
        Phonology.LineSyllables("[chorus]", AssistLanguage.Dutch).ShouldBe(0);
        Phonology.LineSyllables("  ", AssistLanguage.Dutch).ShouldBe(0);
        Phonology.LineSyllables("'k Heb je lief 2", AssistLanguage.Dutch).ShouldBe(3);
        Phonology.WordSyllables("tonight", AssistLanguage.English).ShouldBe(2);
    }

    [Fact]
    public void Report_RhythmTargetOfTheFixture()
    {
        var sheet = AssistFixtures.Expected().GetProperty("sheets").GetProperty("zomeravond");
        var rhythm = sheet.GetProperty("rhythm");
        var target = new RhythmTarget(rhythm.GetProperty("bpm").GetInt32(), rhythm.GetProperty("beatsPerBar").GetInt32(), rhythm.GetProperty("barsPerLine").GetInt32(), rhythm.GetProperty("syllablesPerBeat").GetDouble());

        var report = SyllableReport.Build(AssistFixtures.Sheet("zomeravond.nl.txt"), AssistLanguage.Dutch, target, Phonology);

        target.SyllablesPerLine.ShouldBe(rhythm.GetProperty("targetSyllables").GetInt32());
        report.Lines[1].Syllables.ShouldBe(rhythm.GetProperty("syllablesLine2").GetInt32());
        report.Lines.Where(l => l.Deviates).Select(l => l.Line).ShouldContain(2);
        report.Lines[0].ShouldSatisfyAllConditions(l => l.IsMarker.ShouldBeTrue(), l => l.Section.ShouldBe("verse"), l => l.Target.ShouldBeNull());
        report.TargetDurationSeconds!.Value.ShouldBe(report.Lines.Count(l => !l.IsMarker) * target.SecondsPerLine, 0.001);
        report.WordsPerSecond.ShouldNotBeNull();
    }

    [Fact]
    public void Report_SectionMediansWithoutTarget()
    {
        var report = SyllableReport.Build(AssistFixtures.Sheet("radio.en.txt"), AssistLanguage.English, null, Phonology);

        (report.Lines[1].SectionMedian, report.Lines[2].Syllables, report.Lines[2].Deviates).ShouldBe((10, 12, false));
        report.Lines.Where(l => l.Deviates).ShouldBeEmpty();
        (report.TargetDurationSeconds, report.WordsPerSecond).ShouldBe((null, null));
        SyllableReport.Build("one two\nthree\n\nfour five six seven", AssistLanguage.English, null, Phonology).Lines.Select(l => l.SectionMedian).ShouldBe([1.5, 1.5, null, 5]);
    }

    private static string Wire(RhymeKind? kind) => kind switch
    {
        RhymeKind.Perfect => "perfect",
        RhymeKind.Near => "near",
        _ => "none",
    };

    private static List<(string Word, int Count, string Note)> Gold(string file) => AssistFixtures.Read(file).Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1)
        .Select(line => line.Split('\t')).Select(parts => (parts[0], int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), parts.Length > 2 ? parts[2] : string.Empty)).ToList();

    private sealed class FileLexicon : IPronunciationLexicon
    {
        private readonly Dictionary<string, List<IReadOnlyList<string>>> _entries = new(StringComparer.Ordinal);

        public FileLexicon(string path)
        {
            foreach (var parts in File.ReadLines(path).Select(line => line.Split('#')[0].Split(' ', StringSplitOptions.RemoveEmptyEntries)).Where(parts => parts.Length > 1))
            {
                var word = parts[0].Split('(')[0];
                if (!_entries.TryGetValue(word, out var list))
                {
                    _entries[word] = list = [];
                }

                list.Add(parts[1..]);
            }
        }

        public IReadOnlyList<IReadOnlyList<string>> Pronunciations(string word) => _entries.TryGetValue(word.ToLowerInvariant(), out var found) ? found : [];
    }
}

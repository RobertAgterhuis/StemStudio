using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Domain.Tests.Lyrics.Assist;

/// <summary>023 T025: licence lexicon, code-switch, sound spelling, dictionary misses and the suggestion filter (FR-004, FR-009 - FR-012).</summary>
public sealed class DeterministicLayerTests
{
    private static readonly FixtureSpelling Spelling = new(
        nl: ["de", "wind", "zacht", "door", "het", "hoge", "riet", "heb", "je", "lief", "zo", "stil", "als", "licht", "lopen", "gij", "zijt", "fluistert", "vergeten", "ik", "zal", "nacht", "nooit", "deze"],
        en: ["the", "wind", "is", "soft", "love", "wait", "come", "home", "running", "until", "night", "you", "no", "yeah"]);

    [Fact]
    public void Tokens_KeepApostrophesAndHyphens()
    {
        LyricTokens.Of("'k Heb je lief, zo stil als 't licht").Select(t => t.Text).ShouldBe(["'k", "Heb", "je", "lief", "zo", "stil", "als", "'t", "licht"]);
        LyricTokens.Of("Runnin' home, rock-n-roll 2 night").Select(t => (t.Start, t.Text)).ShouldBe([(0, "Runnin'"), (8, "home"), (14, "rock-n-roll"), (26, "2"), (28, "night")]);
        LyricTokens.Key("Don’t").ShouldBe("don't");
    }

    [Theory]
    [InlineData("'k", AssistLanguage.Dutch, SuggestionType.LicenceElision)]
    [InlineData("Gij", AssistLanguage.Dutch, SuggestionType.LicenceArchaism)]
    [InlineData("effe", AssistLanguage.Dutch, SuggestionType.LicenceSlang)]
    [InlineData("gonna", AssistLanguage.English, SuggestionType.LicenceSlang)]
    [InlineData("'til", AssistLanguage.English, SuggestionType.LicenceElision)]
    [InlineData("Thou", AssistLanguage.English, SuggestionType.LicenceArchaism)]
    public void Lexicon_RecognisesLicences(string word, AssistLanguage language, SuggestionType expected) =>
        LicenceLexicon.TypeOf(word, language).ShouldBe(expected);

    [Fact]
    public void FormRules_DroppedFinalNAndDroppedG()
    {
        LicenceLexicon.Recognise("lope", AssistLanguage.Dutch, Spelling).ShouldBe(SuggestionType.LicenceElision);
        LicenceLexicon.Recognise("Runnin'", AssistLanguage.English, Spelling).ShouldBe(SuggestionType.LicenceElision);
        LicenceLexicon.Recognise("vergeeten", AssistLanguage.Dutch, Spelling).ShouldBeNull();
        LicenceLexicon.Recognise("walkin'", AssistLanguage.English, Spelling).ShouldBeNull();
        LicenceLexicon.TypeOf("tafel", AssistLanguage.Dutch).ShouldBeNull();
    }

    [Fact]
    public void Check_SplitsLicencesFromMisses()
    {
        var check = new DictionaryCheck(Spelling, ["Zelmara"]);
        var text = "[verse]\nde wind fluisterd zacht\n'k Heb je lief, zo stil als 't licht\n\nGij zijt love, zooooo yeahhh\nZelmara 2 nacht\nvergeeten lope";

        var result = check.Run(text, AssistLanguage.Dutch, cancellationToken: TestContext.Current.CancellationToken);

        result.Misses.Select(m => (m.Line, m.Start, m.Word)).ShouldBe([(2, 8, "fluisterd"), (7, 0, "vergeeten")]);
        result.Misses[0].Suggestions.ShouldBe(["fluistert"]);
        result.Notes.Select(n => (n.Anchor.Original, n.Type)).ShouldBe([
            ("'k", SuggestionType.LicenceElision), ("'t", SuggestionType.LicenceElision), ("Gij", SuggestionType.LicenceArchaism),
            ("zijt", SuggestionType.LicenceArchaism), ("love", SuggestionType.LicenceCodeSwitch), ("zooooo", SuggestionType.LicenceSoundSpelling),
            ("yeahhh", SuggestionType.LicenceSoundSpelling), ("lope", SuggestionType.LicenceElision),
        ]);
        result.Notes.ShouldAllBe(n => n.Severity == SuggestionSeverity.Idea && n.Operation == SuggestionOperation.None && n.Source == SuggestionSource.Rules);
    }

    [Fact]
    public void Check_RangeAndPersonalWordsAtSentenceStart()
    {
        var check = new DictionaryCheck(Spelling, ["zelmara"]);

        var result = check.Run("Zelmara\nxyzzy\nqwerty", AssistLanguage.English, (2, 9), TestContext.Current.CancellationToken);

        result.Misses.Select(m => m.Word).ShouldBe(["xyzzy", "qwerty"]);
        result.Notes.ShouldBeEmpty();
        check.Run("ooh na na", AssistLanguage.English, cancellationToken: TestContext.Current.CancellationToken).ShouldSatisfyAllConditions(
            r => r.Misses.ShouldBeEmpty(), r => r.Notes.Count.ShouldBe(3));
    }

    [Fact]
    public void MissDrafts_UnknownTypoAndLicence()
    {
        var miss = new WordMiss(2, 8, "fluisterd", ["fluistert", "fluisteren"]);

        var unknown = DictionaryCheck.Unknown(miss, AssistLanguage.Dutch);
        (unknown.Type, unknown.Severity, unknown.Operation, unknown.Proposal, unknown.Source).ShouldBe(
            (SuggestionType.SpellingUnknownWord, SuggestionSeverity.Improvement, SuggestionOperation.Replace, "fluistert", SuggestionSource.Dictionary));
        unknown.Explanation.ShouldContain("fluistert, fluisteren");
        DictionaryCheck.Unknown(miss with { Suggestions = [] }, AssistLanguage.English).ShouldSatisfyAllConditions(
            d => d.Operation.ShouldBe(SuggestionOperation.None), d => d.Explanation.ShouldBe("The dictionary does not know this word."));
        var typo = DictionaryCheck.Typo(miss, "fluistert", AssistLanguage.English, "qwen3.5:9b");
        (typo.Severity, typo.Source, typo.Model, typo.Anchor).ShouldBe((SuggestionSeverity.Error, SuggestionSource.Model, "qwen3.5:9b", new Anchor(2, 8, 9, "fluisterd")));
        (DictionaryCheck.Licence(miss, SuggestionType.LicenceSlang, AssistLanguage.Dutch) with { Source = SuggestionSource.Model, Model = "m" }).Severity.ShouldBe(SuggestionSeverity.Idea);
    }

    [Theory]
    [InlineData(SuggestionType.LicenceElision)]
    [InlineData(SuggestionType.LicenceArchaism)]
    [InlineData(SuggestionType.LicenceCodeSwitch)]
    [InlineData(SuggestionType.LicenceSoundSpelling)]
    [InlineData(SuggestionType.LicenceSlang)]
    public void LicenceTexts_ExistInBothLanguages(SuggestionType type)
    {
        AssistTexts.Licence(AssistLanguage.Dutch, type).ShouldContain("Geen fout");
        AssistTexts.Licence(AssistLanguage.English, type).ShouldContain("Not a mistake");
        AssistTexts.Typo(AssistLanguage.Dutch, "x").ShouldContain("Spelfout");
        AssistTexts.Unknown(AssistLanguage.Dutch, []).ShouldNotContain("?");
    }

    [Theory]
    [InlineData(Sensitivity.Loose, 1)]
    [InlineData(Sensitivity.Balanced, 2)]
    [InlineData(Sensitivity.Strict, 3)]
    public void Filter_SensitivityOfAChecks(Sensitivity sensitivity, int kept)
    {
        var context = Context(AssistKind.Check, sensitivity);
        var drafts = new[] { Draft(SuggestionSeverity.Error), Draft(SuggestionSeverity.Improvement), Draft(SuggestionSeverity.Idea, SuggestionType.LicenceElision) };

        drafts.Count(d => SuggestionFilter.Keep(d, context)).ShouldBe(kept);
    }

    [Fact]
    public void Filter_OtherKindsIgnoreSensitivity() =>
        SuggestionFilter.Keep(Draft(SuggestionSeverity.Idea, SuggestionType.DeviceEndRhyme), Context(AssistKind.Devices, Sensitivity.Loose)).ShouldBeTrue();

    [Fact]
    public void Filter_NeverTouchesMarkersBlanksOrMissingLines()
    {
        var context = Context(AssistKind.Check, Sensitivity.Strict) with { Text = "[chorus]\n\nde wind" };

        SuggestionFilter.Keep(Draft(SuggestionSeverity.Error) with { Anchor = new Anchor(1, 1, 6, "chorus") }, context).ShouldBeFalse();
        SuggestionFilter.Keep(Draft(SuggestionSeverity.Error) with { Anchor = new Anchor(2, 0, 0, "") }, context).ShouldBeFalse();
        SuggestionFilter.Keep(Draft(SuggestionSeverity.Error) with { Anchor = new Anchor(9, 0, 2, "de") }, context).ShouldBeFalse();
        SuggestionFilter.Keep(Draft(SuggestionSeverity.Error) with { Anchor = new Anchor(3, 0, 2, "de") }, context).ShouldBeTrue();
    }

    [Fact]
    public void Filter_RecognisedLicenceIsNeverAnError()
    {
        var context = Context(AssistKind.Check, Sensitivity.Strict) with { Text = "'k heb 't" };

        SuggestionFilter.Keep(Draft(SuggestionSeverity.Error) with { Anchor = new Anchor(1, 0, 2, "'k") }, context).ShouldBeFalse();
        SuggestionFilter.Keep(Draft(SuggestionSeverity.Error, SuggestionType.LicenceElision) with { Anchor = new Anchor(1, 7, 2, "'t") }, context).ShouldBeFalse();
        SuggestionFilter.Keep(Draft(SuggestionSeverity.Improvement) with { Anchor = new Anchor(1, 0, 2, "'k") }, context).ShouldBeTrue();
        SuggestionFilter.Keep(Draft(SuggestionSeverity.Error) with { Anchor = new Anchor(1, 3, 3, "heb") }, context).ShouldBeTrue();
    }

    [Fact]
    public void Filter_RespectsMarksInChecksAndDevicesOnly()
    {
        var mark = IntentionalMark.Create(Guid.NewGuid(), new MarkContent(IntentionalScope.Word, "wind", null), AssistFixtures.Now);
        var check = Context(AssistKind.Check, Sensitivity.Strict) with { Marks = [mark] };
        var draft = Draft(SuggestionSeverity.Error) with { Anchor = new Anchor(1, 3, 4, "wind") };

        SuggestionFilter.Keep(draft, check).ShouldBeFalse();
        SuggestionFilter.Keep(draft, check with { Kind = AssistKind.Devices }).ShouldBeFalse();
        SuggestionFilter.Keep(draft with { Type = SuggestionType.WriteAlternative }, check with { Kind = AssistKind.Alternatives }).ShouldBeTrue();
    }

    [Fact]
    public void Filter_RespectsPersonalWords()
    {
        var context = Context(AssistKind.Check, Sensitivity.Strict) with { Text = "Zelmara zingt", PersonalWords = ["zelmara"] };

        SuggestionFilter.Keep(Draft(SuggestionSeverity.Improvement) with { Anchor = new Anchor(1, 0, 7, "Zelmara") }, context).ShouldBeFalse();
        SuggestionFilter.Keep(Draft(SuggestionSeverity.Improvement, SuggestionType.GrammarOther) with { Anchor = new Anchor(1, 0, 7, "Zelmara") }, context).ShouldBeTrue();
        SuggestionFilter.Keep(Draft(SuggestionSeverity.Improvement) with { Anchor = new Anchor(1, 0, 0, "") }, context).ShouldBeTrue();
    }

    [Fact]
    public void Filter_TitleAnchorsOnlyForTitles()
    {
        var context = Context(AssistKind.Titles, Sensitivity.Balanced);

        SuggestionFilter.Keep(Draft(SuggestionSeverity.Idea, SuggestionType.WriteTitle) with { Anchor = new Anchor(0, 0, 4, "Song") }, context).ShouldBeTrue();
        SuggestionFilter.Keep(Draft(SuggestionSeverity.Idea) with { Anchor = new Anchor(0, 0, 4, "Song") }, context).ShouldBeFalse();
    }

    [Fact]
    public void Check_IsBoundedAgainstHostileText()
    {
        var counting = new CountingSpelling();
        var text = string.Join('\n', Enumerable.Range(0, 300).Select(i => $"zzq{i} zzqrepeat {new string('x', 41)}"));

        var result = new DictionaryCheck(counting, []).Run(text, AssistLanguage.English, cancellationToken: TestContext.Current.CancellationToken);

        result.Misses.Count.ShouldBe(DictionaryCheck.MaxMisses);
        result.Misses.ShouldAllBe(m => m.Word.Length <= DictionaryCheck.MaxWordLength);
        counting.Suggested.Count(w => w == "zzqrepeat").ShouldBe(1);
        Should.Throw<OperationCanceledException>(() => new DictionaryCheck(counting, []).Run(text, AssistLanguage.English, cancellationToken: new CancellationToken(true)));
    }

    private sealed class CountingSpelling : ISpellChecker
    {
        public List<string> Suggested { get; } = [];

        public bool IsKnown(string word, AssistLanguage language) => false;

        public IReadOnlyList<string> Suggest(string word, AssistLanguage language, int max)
        {
            Suggested.Add(word);
            return [];
        }
    }

    private static FilterContext Context(AssistKind kind, Sensitivity sensitivity) => new(kind, sensitivity, "de wind", [], [], AssistLanguage.Dutch);

    private static SuggestionDraft Draft(SuggestionSeverity severity, SuggestionType type = SuggestionType.SpellingTypo) => new()
    {
        Type = type,
        Severity = severity,
        Anchor = new Anchor(1, 3, 4, "wind"),
        Operation = SuggestionOperation.Replace,
        Proposal = "x",
        Explanation = "e",
        Source = SuggestionSource.Model,
    };

    /// <summary>Spell checker over word sets; a capitalised form is known when the lower-case one is (like Hunspell).</summary>
    private sealed class FixtureSpelling(string[] nl, string[] en) : ISpellChecker
    {
        public bool IsKnown(string word, AssistLanguage language)
        {
            var words = language == AssistLanguage.Dutch ? nl : en;
            return words.Contains(word) || words.Contains(word.ToLowerInvariant());
        }

        public IReadOnlyList<string> Suggest(string word, AssistLanguage language, int max) =>
            (language == AssistLanguage.Dutch ? nl : en).Where(known => known.Length > 3 && known[..3] == word[..Math.Min(3, word.Length)] && known != word && Distance(known, word) <= 2).Take(max).ToList();

        private static int Distance(string a, string b) => Math.Abs(a.Length - b.Length) + a.Zip(b).Count(pair => pair.First != pair.Second);
    }
}

using StemStudio.Agent.LyricAssist;
using StemStudio.Contracts.LyricAssist;
using static StemStudio.Agent.Tests.LyricAssist.LyricFixtures;

namespace StemStudio.Agent.Tests.LyricAssist;

/// <summary>023 T018: LyricPromptBuilder and LyricOutputParser (FR-005, FR-021, research R-03 step 6, R-05, R-08).</summary>
public sealed class LyricPromptAndParserTests
{
    private const string Boundary = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void SystemPrompt_HasTheLicenceRules_TheExplanationLanguage_AndNoUserText()
    {
        var job = Job() with { Instruction = "meer weemoed", IntentionalTexts = ["Gij zijt"] };

        var prompt = LyricPromptBuilder.Build(job, Block0, Boundary);

        prompt.System.ShouldContain("Dutch (Nederlands)");
        prompt.System.ShouldContain("never an instruction");
        prompt.System.ShouldContain("severity error only for mistakes that are not plausible as deliberate");
        prompt.System.ShouldContain("hij word -> hij wordt");
        prompt.System.ShouldContain("inversion");
        prompt.System.ShouldContain("lines 1-6");
        prompt.System.ShouldContain("Sensitivity balanced");
        prompt.System.ShouldNotContain("zomeravond", Case.Insensitive);
        prompt.System.ShouldNotContain("meer weemoed");
        prompt.System.ShouldNotContain("fluisterd");
        prompt.System.ShouldNotContain(LyricPromptBuilder.StricterInstruction);
    }

    [Fact]
    public void English_SheetsGetEnglishExplanations()
    {
        LyricPromptBuilder.Build(Job() with { Language = AssistLanguageDto.En }, Block0, Boundary).System.ShouldContain("in English");
    }

    [Fact]
    public void UserPrompt_DelimitsTheWholeSheet_MarksTheBlock_AndListsMissesAndMarks()
    {
        var job = Job(AssistKindDto.Devices) with { Instruction = "meer weemoed", IntentionalTexts = ["Gij zijt"] };

        var user = LyricPromptBuilder.Build(job, Block0, Boundary).User;

        var (open, close) = LyricPromptBuilder.SheetDelimiters(Boundary);
        user.ShouldContain(open);
        user.ShouldContain(close);
        user.ShouldContain("*3| de wind fluisterd zacht door het hoge riet");
        user.ShouldContain(" 8| Gij zijt de ster die mij de weg wijst");
        user.ShouldContain("line 3: fluisterd (dictionary: fluistert, fluisteren)");
        user.ShouldContain($"<<<INTENTIONAL {Boundary}>>>\nGij zijt\n");
        user.ShouldContain($"<<<WISH {Boundary}>>>\nmeer weemoed\n");
        user.ShouldContain($"<<<TITLE {Boundary}>>>\nZomeravond\n");
        user.IndexOf(open, StringComparison.Ordinal).ShouldBeLessThan(user.IndexOf("De zomeravond", StringComparison.Ordinal));
    }

    [Fact]
    public void ABoundaryInsideTheData_IsRefused_AndNewBoundariesAreRandom()
    {
        var job = Job() with { Text = Text + "\n" + Boundary };

        Should.Throw<ArgumentException>(() => LyricPromptBuilder.Build(job, Block0, Boundary));
        LyricPromptBuilder.NewBoundary().ShouldNotBe(LyricPromptBuilder.NewBoundary());
        LyricPromptBuilder.NewBoundary().Length.ShouldBe(32);
    }

    [Fact]
    public void TheRetry_AddsTheStricterInstruction()
    {
        LyricPromptBuilder.Build(Job(), Block0, Boundary, strict: true).System.ShouldEndWith(LyricPromptBuilder.StricterInstruction);
    }

    [Theory]
    [InlineData(AssistKindDto.Check, 0.2, "check lines")]
    [InlineData(AssistKindDto.Devices, 0.2, "poetic devices")]
    [InlineData(AssistKindDto.Alternatives, 0.8, "alternative versions of line 9")]
    [InlineData(AssistKindDto.Continue, 0.8, "continuation of 2 new lines after line 9")]
    [InlineData(AssistKindDto.Titles, 0.8, "song titles")]
    public void EveryKind_HasItsTaskAndTemperature(AssistKindDto kind, double temperature, string task)
    {
        var job = Job(kind) with { LineFrom = 9, LineTo = 9, RhythmTarget = new RhythmTargetDto { Bpm = 90, BeatsPerBar = 4, BarsPerLine = 2, SyllablesPerBeat = 1.5 } };

        var prompt = LyricPromptBuilder.Build(job, Block1, Boundary);

        prompt.System.ShouldContain(task);
        prompt.System.ShouldContain("about 12 syllables per line");
        LyricPromptBuilder.Temperature(kind).ShouldBe(temperature);
    }

    [Theory]
    [InlineData(SensitivityDto.Loose, "only clear errors")]
    [InlineData(SensitivityDto.Strict, "recognised licences")]
    public void Sensitivity_IsAHint(SensitivityDto sensitivity, string hint) =>
        LyricPromptBuilder.Build(Job() with { Sensitivity = sensitivity }, Block0, Boundary).System.ShouldContain(hint);

    [Fact]
    public void Parser_ReadsASchemaValidOutput()
    {
        var output = LyricOutputParser.Parse("""
            {"items":[{"type":"grammar.verb-form","severity":"error","line":10,"original":"word","proposal":"wordt","explanation":"Derde persoon.","rhymeWithLine":null,"rhymeWord":null}],
             "misses":[{"line":3,"word":"fluisterd","verdict":"typo","correction":"fluistert","licenceType":null},{"line":15,"word":"Zelmara","verdict":"keep"}]}
            """)!;

        output.InvalidItems.ShouldBe(0);
        output.Items.Single().ShouldBe(new ModelSuggestionItem { Type = SuggestionTypeDto.GrammarVerbForm, Severity = SuggestionSeverityDto.Error, Line = 10, Original = "word", Proposal = "wordt", Explanation = "Derde persoon." });
        output.Misses.Select(miss => miss.Verdict).ShouldBe([MissVerdict.Typo, MissVerdict.Keep]);
        output.Misses[0].Correction.ShouldBe("fluistert");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"misses":[]}""")]
    [InlineData("""{"items":{}}""")]
    [InlineData("""{"items":[],"extra":1}""")]
    [InlineData("""{"items":[],"misses":{}}""")]
    [InlineData("""{"items":[{"type":"grammar.other","severity":"idea","line":1,"original":"x","explanation":"y"}] """)]
    public void Parser_RejectsAnInvalidOutputAsAWhole(string json) => LyricOutputParser.Parse(json).ShouldBeNull();

    [Fact]
    public void Parser_RejectsOversizeArrays()
    {
        var items = string.Join(',', Enumerable.Repeat("""{"type":"grammar.other","severity":"idea","line":1,"original":"x","explanation":"y"}""", 41));

        LyricOutputParser.Parse($$"""{"items":[{{items}}]}""").ShouldBeNull();
        LyricOutputParser.Parse($$"""{"items":[],"misses":[{{string.Join(',', Enumerable.Repeat("""{"line":1,"word":"a","verdict":"keep"}""", 41))}}]}""").ShouldBeNull();
    }

    [Theory]
    [InlineData("""{"type":"spelling.unknown","severity":"error","line":1,"original":"x","explanation":"y"}""")]
    [InlineData("""{"type":"spelling.typo","severity":"fatal","line":1,"original":"x","explanation":"y"}""")]
    [InlineData("""{"type":"spelling.typo","severity":"error","line":-1,"original":"x","explanation":"y"}""")]
    [InlineData("""{"type":"spelling.typo","severity":"error","line":1.5,"original":"x","explanation":"y"}""")]
    [InlineData("""{"type":"spelling.typo","severity":"error","line":1,"explanation":"y"}""")]
    [InlineData("""{"type":"spelling.typo","severity":"error","line":1,"original":"x","explanation":""}""")]
    [InlineData("""{"type":"spelling.typo","severity":"error","line":1,"original":"x","explanation":"y","extra":true}""")]
    [InlineData("""{"type":"spelling.typo","severity":"error","line":1,"original":"x","explanation":"y","proposal":5}""")]
    [InlineData("""{"type":"spelling.typo","severity":"error","line":1,"original":"x","explanation":"y","rhymeWithLine":0}""")]
    [InlineData("""{"type":"spelling.typo","severity":"error","line":1,"original":"x","explanation":"y","rhymeWord":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}""")]
    [InlineData("""{"type":1,"severity":"error","line":1,"original":"x","explanation":"y"}""")]
    [InlineData("\"item\"")]
    public void Parser_DropsAndCountsAnItemThatBreaksTheSchema(string item)
    {
        var output = LyricOutputParser.Parse($$"""{"items":[{{item}},{"type":"grammar.other","severity":"idea","line":1,"original":"x","explanation":"y"}]}""")!;

        output.Items.Count.ShouldBe(1);
        output.InvalidItems.ShouldBe(1);
    }

    [Fact]
    public void Parser_DropsTooLongTexts()
    {
        var output = LyricOutputParser.Parse($$"""{"items":[{"type":"grammar.other","severity":"idea","line":1,"original":"x","proposal":"{{new string('a', 401)}}","explanation":"y"},{"type":"grammar.other","severity":"idea","line":1,"original":"x","explanation":"{{new string('a', 301)}}"}]}""")!;

        output.Items.ShouldBeEmpty();
        output.InvalidItems.ShouldBe(2);
    }

    [Theory]
    [InlineData("""{"line":0,"word":"a","verdict":"keep"}""")]
    [InlineData("""{"line":1,"word":"","verdict":"keep"}""")]
    [InlineData("""{"line":1,"word":"a","verdict":"maybe"}""")]
    [InlineData("""{"line":1,"word":"a","verdict":"licence","licenceType":"poetry"}""")]
    [InlineData("""{"line":1,"word":"a","verdict":"typo","correction":7}""")]
    [InlineData("""{"line":1,"word":"a","verdict":"keep","why":"x"}""")]
    public void Parser_DropsAndCountsAMissThatBreaksTheSchema(string miss)
    {
        var output = LyricOutputParser.Parse($$"""{"items":[],"misses":[{{miss}},{"line":1,"word":"a","verdict":"licence","licenceType":"elision"}]}""")!;

        output.Misses.Single().LicenceType.ShouldBe("elision");
        output.InvalidItems.ShouldBe(1);
    }

    [Fact]
    public void LyricText_KnowsMarkersWordsAndLines()
    {
        var text = new LyricText("[verse 2]\na b\r\n\n'k zie   de-ster’s");

        LyricText.IsMarker("[chorus]").ShouldBeTrue();
        LyricText.IsMarker("[chorus] la").ShouldBeFalse();
        text.IsReviewable(1).ShouldBeFalse();
        text.IsReviewable(2).ShouldBeTrue();
        text.IsReviewable(3).ShouldBeFalse();
        text.IsReviewable(9).ShouldBeFalse();
        text.Line(0).ShouldBeNull();
        LyricText.Words(text.Line(4)).ShouldBe(["k", "zie", "de-ster’s"]);
        LyricText.LastWord("...").ShouldBeNull();
    }

    [Fact]
    public void LyricTypes_MapNamesAndCategories()
    {
        LyricTypes.Parse("flow.syllable-count").ShouldBe(SuggestionTypeDto.FlowSyllableCount);
        LyricTypes.Parse("nope").ShouldBeNull();
        LyricTypes.Parse(null).ShouldBeNull();
        LyricTypes.Category(SuggestionTypeDto.LicenceCodeSwitch).ShouldBe(SuggestionCategoryDto.Licence);
        LyricTypes.Category(SuggestionTypeDto.WriteTitle).ShouldBe(SuggestionCategoryDto.Title);
        LyricTypes.Category(SuggestionTypeDto.WriteContinuation).ShouldBe(SuggestionCategoryDto.Continuation);
        LyricTypes.Category(SuggestionTypeDto.WriteAlternative).ShouldBe(SuggestionCategoryDto.Alternative);
        LyricTypes.Allowed(AssistKindDto.Check, [SuggestionCategoryDto.Spelling]).ShouldBe([SuggestionCategoryDto.Spelling]);
        LyricTypes.Allowed(AssistKindDto.Check, [SuggestionCategoryDto.Device]).ShouldBe(LyricTypes.CheckCategories);
        LyricTypes.Allowed(AssistKindDto.Devices, []).ShouldBe(LyricTypes.DeviceCategories);
    }
}

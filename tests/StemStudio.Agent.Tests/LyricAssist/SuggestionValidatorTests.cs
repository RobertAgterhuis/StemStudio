using StemStudio.Agent.LyricAssist;
using StemStudio.Contracts.LyricAssist;
using static StemStudio.Agent.Tests.LyricAssist.LyricFixtures;

namespace StemStudio.Agent.Tests.LyricAssist;

/// <summary>023 T018 / T040 / T049: SuggestionValidator (anchors, kinds, limits, verification, verbatim, duplicates; FR-013-FR-015, FR-021, FR-023).</summary>
public sealed class SuggestionValidatorTests
{
    private static LyricBlockResult Check(params ModelSuggestionItem[] items) => SuggestionValidator.Validate(Context(Job(), Block1), Output(items));

    [Fact]
    public void AnExactAnchor_BecomesAReplaceWithComputedSyllables()
    {
        var verification = new FakeVerification();
        var result = SuggestionValidator.Validate(Context(Job(), Block1, verification), Output(Item(SuggestionTypeDto.GrammarVerbForm, 10, "word", "wordt")));

        var suggestion = result.Suggestions.Single();
        suggestion.Anchor.ShouldBe(new AnchorDto { Line = 10, Start = 8, Length = 4, Original = "word" });
        suggestion.Operation.ShouldBe(SuggestionOperationDto.Replace);
        suggestion.Category.ShouldBe(SuggestionCategoryDto.Grammar);
        suggestion.Severity.ShouldBe(SuggestionSeverityDto.Error);
        suggestion.Proposal.ShouldBe("wordt");
        suggestion.GroupId.ShouldBeNull();
        suggestion.SyllablesBefore.ShouldBe(verification.CountSyllables("de stad word stil als de klok twaalf slaat", AssistLanguageDto.Nl));
        suggestion.SyllablesAfter.ShouldBe(verification.CountSyllables("de stad wordt stil als de klok twaalf slaat", AssistLanguageDto.Nl));
        result.Discarded.ShouldBe(DiscardedCountsDto.None);
        result.BlockIndex.ShouldBe(1);
    }

    [Fact]
    public void TheOriginal_IsReFoundOnceIgnoringCase_AndTakesTheRealText()
    {
        var suggestion = Check(Item(SuggestionTypeDto.SpellingCapitalisation, 10, "DE STAD", "De stad")).Suggestions.Single();

        suggestion.Anchor.ShouldBe(new AnchorDto { Line = 10, Start = 0, Length = 7, Original = "de stad" });
    }

    [Theory]
    [InlineData(10, "zzz")]
    [InlineData(99, "word")]
    [InlineData(3, "fluisterd")]
    [InlineData(7, "[chorus]")]
    [InlineData(12, " ")]
    [InlineData(10, "")]
    public void AFakeOrForeignAnchor_IsDropped(int line, string original)
    {
        var result = Check(Item(SuggestionTypeDto.SpellingTypo, line, original, "x"));

        result.Suggestions.ShouldBeEmpty();
        result.Discarded.InvalidAnchor.ShouldBe(1);
    }

    [Fact]
    public void TheRequestedRange_LimitsTheAnchors()
    {
        var job = Job() with { LineFrom = 8, LineTo = 9 };

        var result = SuggestionValidator.Validate(Context(job, Block1), Output(Item(SuggestionTypeDto.GrammarVerbForm, 10, "word", "wordt")));

        result.Discarded.InvalidAnchor.ShouldBe(1);
    }

    [Theory]
    [InlineData(SuggestionTypeDto.DeviceAlliteration)]
    [InlineData(SuggestionTypeDto.WriteAlternative)]
    [InlineData(SuggestionTypeDto.WriteTitle)]
    public void ACategoryThatTheKindDoesNotProduce_IsDropped(SuggestionTypeDto type)
    {
        var result = Check(Item(type, 10, "word", "wordt"));

        result.Suggestions.ShouldBeEmpty();
        result.Discarded.InvalidSchema.ShouldBe(1);
    }

    [Fact]
    public void RequestedCategories_NarrowTheCheck()
    {
        var job = Job() with { Categories = [SuggestionCategoryDto.Spelling] };

        var result = SuggestionValidator.Validate(Context(job, Block1), Output(Item(SuggestionTypeDto.GrammarVerbForm, 10, "word", "wordt")));

        result.Discarded.InvalidSchema.ShouldBe(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("word")]
    [InlineData("wordt\nnog een regel")]
    public void ACorrection_NeedsARealSingleLineProposal(string? proposal) =>
        Check(Item(SuggestionTypeDto.GrammarVerbForm, 10, "word", proposal)).Discarded.InvalidSchema.ShouldBe(1);

    [Fact]
    public void ACorrection_MayNotRewriteMoreThan60PercentOfTheLine()
    {
        var line = "de stad word stil als de klok twaalf slaat";

        var rewrite = Check(Item(SuggestionTypeDto.FlowAwkward, 10, line, "DELETE"));
        var small = Check(Item(SuggestionTypeDto.FlowAwkward, 10, line, "de stad wordt stil als de klok twaalf slaat"));

        rewrite.Discarded.InvalidSchema.ShouldBe(1);
        small.Suggestions.Count.ShouldBe(1);
    }

    [Fact]
    public void ALicenceNote_IsInformationOnly()
    {
        var suggestion = Check(Item(SuggestionTypeDto.LicenceArchaism, 8, "Gij zijt", "Jij bent")).Suggestions.Single();

        suggestion.Operation.ShouldBe(SuggestionOperationDto.None);
        suggestion.Proposal.ShouldBeNull();
        suggestion.Severity.ShouldBe(SuggestionSeverityDto.Idea);
        suggestion.SyllablesBefore.ShouldBeNull();
    }

    [Fact]
    public void Duplicates_AreMerged()
    {
        var item = Item(SuggestionTypeDto.GrammarVerbForm, 10, "word", "wordt");

        var result = Check(item, item with { Explanation = "Anders." }, item with { Proposal = "werd" });

        result.Suggestions.Select(suggestion => suggestion.Proposal).ShouldBe(["wordt", "werd"]);
        result.Discarded.ShouldBe(DiscardedCountsDto.None);
    }

    [Fact]
    public void VerbatimLibraryText_IsWithheld()
    {
        var verification = new FakeVerification();
        verification.Library.Add("klok slaat twaalf uur");

        var result = SuggestionValidator.Validate(Context(Job(), Block1, verification), Output(Item(SuggestionTypeDto.FlowAwkward, 10, "twaalf slaat", "klok slaat twaalf uur")));

        result.Suggestions.ShouldBeEmpty();
        result.Discarded.Verbatim.ShouldBe(1);
    }

    [Fact]
    public void SchemaInvalidItemsOfTheParser_AreCounted()
    {
        SuggestionValidator.Validate(Context(Job(), Block1), new ParsedLyricOutput([], [], 3)).Discarded.InvalidSchema.ShouldBe(3);
    }

    [Fact]
    public void MissVerdicts_MustNameAMissOfTheBlock_AndBeComplete()
    {
        ModelMissVerdict[] verdicts =
        [
            new() { Line = 14, Word = "vergeeten", Verdict = MissVerdict.Typo, Correction = "vergeten" },
            new() { Line = 14, Word = "vergeeten", Verdict = MissVerdict.Keep },
            new() { Line = 15, Word = "Zelmara", Verdict = MissVerdict.Licence },
            new() { Line = 15, Word = "Zelmara", Verdict = MissVerdict.Keep },
            new() { Line = 3, Word = "fluisterd", Verdict = MissVerdict.Typo, Correction = "fluistert" },
            new() { Line = 14, Word = "vergeeten", Verdict = MissVerdict.Typo, Correction = "vergeeten" },
        ];

        var result = SuggestionValidator.Validate(Context(Job(), Block2), new ParsedLyricOutput([], verdicts, 0));

        result.Misses.Select(miss => (miss.Word, miss.Verdict)).ShouldBe([("vergeeten", MissVerdict.Typo), ("Zelmara", MissVerdict.Keep)]);
        result.Discarded.InvalidAnchor.ShouldBe(1);
        result.Discarded.InvalidSchema.ShouldBe(2);
    }

    [Fact]
    public void ATypoVerdictWithoutCorrection_IsIncomplete()
    {
        var result = SuggestionValidator.Validate(Context(Job(), Block2), new ParsedLyricOutput([], [new() { Line = 14, Word = "vergeeten", Verdict = MissVerdict.Typo }], 0));

        result.Misses.ShouldBeEmpty();
        result.Discarded.InvalidSchema.ShouldBe(1);
    }

    [Fact]
    public void NullArguments_AreRejected()
    {
        Should.Throw<ArgumentNullException>(() => SuggestionValidator.Validate(null!, Output()));
        Should.Throw<ArgumentNullException>(() => SuggestionValidator.Validate(Context(Job(), Block1), null!));
    }
}

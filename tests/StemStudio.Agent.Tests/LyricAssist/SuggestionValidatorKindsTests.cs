using StemStudio.Agent.LyricAssist;
using StemStudio.Contracts.LyricAssist;
using static StemStudio.Agent.Tests.LyricAssist.LyricFixtures;

namespace StemStudio.Agent.Tests.LyricAssist;

/// <summary>023 T040 (devices: verified rhymes and syllable fit) and T049 (writing kinds: alternatives, continuation, titles).</summary>
public sealed class SuggestionValidatorKindsTests
{
    private static ModelSuggestionItem Rhyme(SuggestionTypeDto type, int line, string original, string? proposal, int? partnerLine, string? partnerWord) =>
        Item(type, line, original, proposal, SuggestionSeverityDto.Idea) with { RhymeWithLine = partnerLine, RhymeWord = partnerWord };

    private static LyricBlockResult Devices(LyricAssistJob? job = null, params ModelSuggestionItem[] items) =>
        SuggestionValidator.Validate(Context(job ?? Job(AssistKindDto.Devices), Block1), Output(items));

    [Fact]
    public void Devices_OnlyDeviceAndLicenceTypes()
    {
        var result = Devices(null,
            Item(SuggestionTypeDto.DeviceImagery, 8, "de ster", null, SuggestionSeverityDto.Error),
            Item(SuggestionTypeDto.LicenceInversion, 9, "Naar huis ik ga", null),
            Item(SuggestionTypeDto.GrammarVerbForm, 10, "word", "wordt"));

        result.Suggestions.Select(suggestion => (suggestion.Category, suggestion.Operation, suggestion.Severity)).ShouldBe(
        [
            (SuggestionCategoryDto.Device, SuggestionOperationDto.None, SuggestionSeverityDto.Idea),
            (SuggestionCategoryDto.Licence, SuggestionOperationDto.None, SuggestionSeverityDto.Idea),
        ]);
        result.Discarded.InvalidSchema.ShouldBe(1);
    }

    [Fact]
    public void AVerifiedEndRhyme_CarriesItsPartner()
    {
        // "slaat" (line 10) and "staat" share the last two letters in the fake rhyme check; the partner word is on line 10.
        var suggestion = Devices(null, Rhyme(SuggestionTypeDto.DeviceEndRhyme, 11, "moe ben", "moe, het is laat", 10, "slaat")).Suggestions.Single();

        suggestion.RhymeWith.ShouldBe(new RhymePartnerDto { Line = 10, Word = "slaat" });
        suggestion.Operation.ShouldBe(SuggestionOperationDto.Replace);
        suggestion.SyllablesAfter.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("moe, zo moe", 10, "slaat")]
    [InlineData("moe, het is laat", 10, "staat")]
    [InlineData("moe, het is laat", 99, "slaat")]
    [InlineData("moe, het is laat", null, "slaat")]
    [InlineData("moe, het is laat", 10, null)]
    [InlineData("moe slaat", 10, "slaat")]
    public void AWrongRhymeClaim_IsDroppedAsFailedVerification(string proposal, int? partnerLine, string? partnerWord)
    {
        var result = Devices(null, Rhyme(SuggestionTypeDto.DeviceEndRhyme, 11, "moe ben", proposal, partnerLine, partnerWord));

        result.Suggestions.ShouldBeEmpty();
        result.Discarded.FailedVerification.ShouldBe(1);
    }

    [Fact]
    public void AnInternalRhyme_MayUseAnyWordOfTheOriginal()
    {
        // No proposal: the original "de straat is leeg" has "straat", which rhymes with "slaat" on line 10.
        var suggestion = Devices(null, Rhyme(SuggestionTypeDto.DeviceInternalRhyme, 9, "de straat is leeg", null, 10, "slaat")).Suggestions.Single();

        suggestion.Operation.ShouldBe(SuggestionOperationDto.None);
        suggestion.RhymeWith!.Line.ShouldBe(10);
    }

    [Fact]
    public void SyllableFit_MustMoveTowardsTheRhythmTarget_WithModelClaimsIgnored()
    {
        var target = new RhythmTargetDto { Bpm = 90, BeatsPerBar = 4, BarsPerLine = 2, SyllablesPerBeat = 1 };
        var job = Job(AssistKindDto.Devices) with { RhythmTarget = target };
        var line = "en het is zo dat ik eigenlijk wel heel erg moe ben";

        var shorter = Devices(job, Item(SuggestionTypeDto.DeviceSyllableFit, 11, "en het is zo dat ik eigenlijk wel heel erg", "ik ben zo", SuggestionSeverityDto.Idea));
        var longer = Devices(job, Item(SuggestionTypeDto.DeviceSyllableFit, 11, "moe ben", "ontzettend vermoeid ben ik nu", SuggestionSeverityDto.Idea));

        shorter.Suggestions.Single().SyllablesBefore.ShouldBe(new FakeVerification().CountSyllables(line, AssistLanguageDto.Nl));
        longer.Discarded.FailedVerification.ShouldBe(1);
    }

    [Fact]
    public void SyllableFit_WithoutATarget_MustChangeTheCount()
    {
        var same = Devices(null, Item(SuggestionTypeDto.DeviceSyllableFit, 10, "stil", "stom", SuggestionSeverityDto.Idea));
        var changed = Devices(null, Item(SuggestionTypeDto.DeviceSyllableFit, 10, "stil", "stiller", SuggestionSeverityDto.Idea));
        var none = Devices(null, Item(SuggestionTypeDto.DeviceSyllableFit, 10, "stil", null, SuggestionSeverityDto.Idea));

        same.Discarded.FailedVerification.ShouldBe(1);
        changed.Suggestions.Count.ShouldBe(1);
        none.Discarded.FailedVerification.ShouldBe(1);
    }

    [Fact]
    public void Alternatives_ShareOneGroup_TargetTheLine_AndStayWithinTheCount()
    {
        var job = Job(AssistKindDto.Alternatives) with { LineFrom = 9, LineTo = 9, Count = 2 };
        var line = "Naar huis ik ga, de straat is leeg";

        var result = SuggestionValidator.Validate(Context(job, Block1), Output(
            Item(SuggestionTypeDto.WriteAlternative, 9, line, "Ik ga naar huis, de straat is leeg", SuggestionSeverityDto.Error),
            Item(SuggestionTypeDto.WriteAlternative, 9, line, "Naar huis, de straten leeg"),
            Item(SuggestionTypeDto.WriteAlternative, 9, line, "Een derde versie"),
            Item(SuggestionTypeDto.WriteAlternative, 10, "de stad word stil", "x")));

        result.Suggestions.Count.ShouldBe(2);
        result.Suggestions.ShouldAllBe(suggestion => suggestion.GroupId == Guid.Parse("99999999-0000-0000-0000-000000000009")
            && suggestion.Category == SuggestionCategoryDto.Alternative && suggestion.Severity == SuggestionSeverityDto.Idea && suggestion.Operation == SuggestionOperationDto.Replace);
        result.Discarded.InvalidSchema.ShouldBe(1);
        result.Discarded.InvalidAnchor.ShouldBe(1);
    }

    [Fact]
    public void AnAlternative_MayRewriteTheWholeLine()
    {
        var job = Job(AssistKindDto.Alternatives) with { LineFrom = 10, LineTo = 10, Count = 1 };

        var result = SuggestionValidator.Validate(Context(job, Block1), Output(Item(SuggestionTypeDto.WriteAlternative, 10, "de stad word stil als de klok twaalf slaat", "Middernacht, de straten zwijgen")));

        result.Suggestions.Single().Proposal.ShouldBe("Middernacht, de straten zwijgen");
    }

    [Fact]
    public void AContinuation_IsInsertedAfterTheLine_WithOneToFourLines()
    {
        var job = Job(AssistKindDto.Continue) with { LineFrom = 8, LineTo = 11, Count = 2 };

        var result = SuggestionValidator.Validate(Context(job, Block1), Output(
            Item(SuggestionTypeDto.WriteContinuation, 11, "", "Ik leg mijn hoofd neer\r\nen droom van de zee"),
            Item(SuggestionTypeDto.WriteContinuation, 11, "", "Nog een\nversie")));

        var suggestion = result.Suggestions.Single();
        suggestion.Anchor.ShouldBe(new AnchorDto { Line = 11, Start = 50, Length = 0, Original = "" });
        suggestion.Operation.ShouldBe(SuggestionOperationDto.InsertAfter);
        suggestion.Proposal.ShouldBe("Ik leg mijn hoofd neer\nen droom van de zee");
        suggestion.SyllablesBefore.ShouldBeNull();
        result.Discarded.InvalidSchema.ShouldBe(1);
    }

    [Theory]
    [InlineData(11, "x", "Een regel")]
    [InlineData(10, "", "Een regel")]
    [InlineData(11, "", "Een\ntwee\ndrie")]
    [InlineData(11, "", "Een\n\ntwee")]
    [InlineData(11, "", "[chorus]")]
    public void AWrongContinuation_IsDropped(int line, string original, string proposal)
    {
        var job = Job(AssistKindDto.Continue) with { LineTo = 11, Count = 2 };

        SuggestionValidator.Validate(Context(job, Block1), Output(Item(SuggestionTypeDto.WriteContinuation, line, original, proposal))).Suggestions.ShouldBeEmpty();
    }

    [Fact]
    public void Titles_SetTheTitle_AtMostTheCount_AndAtMost80Characters()
    {
        var job = Job(AssistKindDto.Titles) with { Count = 2 };

        var result = SuggestionValidator.Validate(Context(job, Block0), Output(
            Item(SuggestionTypeDto.WriteTitle, 0, "Zomeravond", "Over de oude dijk"),
            Item(SuggestionTypeDto.WriteTitle, 0, "Zomeravond", new string('a', 81)),
            Item(SuggestionTypeDto.WriteTitle, 0, "Andere titel", "Riet"),
            Item(SuggestionTypeDto.WriteTitle, 1, "Zomeravond", "Riet"),
            Item(SuggestionTypeDto.WriteTitle, 0, "Zomeravond", "Twaalf slagen"),
            Item(SuggestionTypeDto.WriteTitle, 0, "Zomeravond", "Te veel")));

        result.Suggestions.Select(suggestion => suggestion.Proposal).ShouldBe(["Over de oude dijk", "Twaalf slagen"]);
        result.Suggestions[0].Anchor.ShouldBe(new AnchorDto { Line = 0, Start = 0, Length = 10, Original = "Zomeravond" });
        result.Suggestions[0].Operation.ShouldBe(SuggestionOperationDto.SetTitle);
        result.Discarded.InvalidAnchor.ShouldBe(2);
        result.Discarded.InvalidSchema.ShouldBe(2);
    }

    [Fact]
    public void EveryGeneratedText_PassesTheVerbatimGuard()
    {
        var verification = new FakeVerification();
        verification.Library.Add("droom van de zee");
        var job = Job(AssistKindDto.Continue) with { LineTo = 11, Count = 2 };

        var result = SuggestionValidator.Validate(Context(job, Block1, verification), Output(Item(SuggestionTypeDto.WriteContinuation, 11, "", "Ik leg mijn hoofd neer\nen droom van de zee")));

        result.Suggestions.ShouldBeEmpty();
        result.Discarded.Verbatim.ShouldBe(1);
    }
}

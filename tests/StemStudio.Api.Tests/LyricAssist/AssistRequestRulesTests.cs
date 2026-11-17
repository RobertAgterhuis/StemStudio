using StemStudio.Api.LyricAssist;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Api.Tests.LyricAssist;

/// <summary>Field rules and defaults of a create request (data-model 3), incl. the integration additions (origin, undefined enum numbers).</summary>
public sealed class AssistRequestRulesTests
{
    private static readonly LyricsSheet Sheet = LyricsSheet.Create(Guid.NewGuid(), new LyricsContent("Song", "en", "[verse]\none\ntwo\nthree"), DateTime.UnixEpoch);

    private static CreateLyricAssistRequest Check => new() { Kind = AssistKindDto.Check, ExpectedRevision = 1 };

    [Fact]
    public void UndefinedEnumNumbers_AreOneKindError()
    {
        AssistRequestRules.Validate(Check with { Kind = (AssistKindDto)99 }, Sheet).Single().Field.ShouldBe("kind");
        AssistRequestRules.Validate(Check with { Categories = [(SuggestionCategoryDto)42] }, Sheet).Single().Field.ShouldBe("kind");
        AssistRequestRules.Validate(Check with { Origin = (AssistOriginDto)7 }, Sheet).Single().Field.ShouldBe("kind");
        AssistRequestRules.Validate(Check with { Origin = AssistOriginDto.Editor, Categories = [SuggestionCategoryDto.Spelling] }, Sheet).ShouldBeEmpty();
    }

    [Fact]
    public void Origin_MapsToTheDomain_AndTheRunIdNeedsTheAgent()
    {
        var run = Guid.NewGuid();

        AssistRequestRules.ToDomain(Check, Sheet).ShouldSatisfyAllConditions(r => r.Origin.ShouldBe(AssistOrigin.Editor), r => r.AgentRunId.ShouldBeNull());
        AssistRequestRules.ToDomain(Check with { Origin = AssistOriginDto.Agent, AgentRunId = run }, Sheet)
            .ShouldSatisfyAllConditions(r => r.Origin.ShouldBe(AssistOrigin.Agent), r => r.AgentRunId.ShouldBe(run));
        AssistRequestRules.Validate(Check with { Origin = AssistOriginDto.Editor, AgentRunId = run }, Sheet).Single().Field.ShouldBe("agentRunId");
    }

    [Fact]
    public void Continue_DefaultsToTheEndOfTheSheet_AndKeepsATrimmedInstruction()
    {
        var request = AssistRequestRules.ToDomain(new CreateLyricAssistRequest { Kind = AssistKindDto.Continue, ExpectedRevision = 1, Instruction = "  warmer  " }, Sheet);

        (request.LineTo, request.Instruction, request.Count).ShouldBe((4, "warmer", 2));
        AssistRequestRules.ToDomain(Check with { Instruction = "   " }, Sheet).Instruction.ShouldBeNull();
    }
}

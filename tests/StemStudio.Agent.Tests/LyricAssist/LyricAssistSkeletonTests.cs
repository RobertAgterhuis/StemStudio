using StemStudio.Agent.LyricAssist;
using StemStudio.Agent.Tools.Providers;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.Tests.LyricAssist;

/// <summary>023 Phase 0 (T007): skeleton values the lanes build on (tool names of catalogue 1.2.0, defaults, failure).</summary>
public sealed class LyricAssistSkeletonTests
{
    [Fact]
    public void ToolNames_AreTheEightCatalogueTools()
    {
        LyricAssistToolNames.All.ShouldBe(
        [
            "check_lyrics", "suggest_lyric_lines", "list_lyric_suggestions", "apply_lyric_suggestions", "mark_lyrics_intentional",
            "find_rhymes", "count_syllables", "open_lyrics_editor",
        ]);
        LyricAssistToolNames.Group.ShouldBe("lyric-assist");
    }

    [Fact]
    public void Provider_HasTheEightToolsOfTheCatalogue()
    {
        new LyricAssistToolProvider().GetTools().Select(tool => tool.Name).ShouldBe(LyricAssistToolNames.All);
    }

    [Fact]
    public void BlockResult_DefaultsToNothingDiscarded()
    {
        var result = new LyricBlockResult { BlockIndex = 2 };

        (result.Skipped, result.Suggestions.Count, result.Misses.Count, result.Discarded).ShouldBe((false, 0, 0, DiscardedCountsDto.None));
    }

    [Fact]
    public void Exception_CarriesCodeAndFix()
    {
        var error = new LyricAssistException(AssistFailureCodeDto.BudgetExhausted, "Token cap reached.", "Raise the cap in Settings.");

        (error.Code, error.Message, error.Fix).ShouldBe((AssistFailureCodeDto.BudgetExhausted, "Token cap reached.", "Raise the cap in Settings."));
        new LyricAssistException(AssistFailureCodeDto.Timeout, "Timed out.").Fix.ShouldBeNull();
    }
}

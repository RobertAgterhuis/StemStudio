using Bunit;
using NSubstitute;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using static StemStudio.Web.Tests.Fakes.LyricAssistData;

namespace StemStudio.Web.Tests.Songs;

/// <summary>023 T063: Claude notice, unavailable model, fallback, usage, assist log and revisions (FR-026, FR-027, FR-029).</summary>
public sealed class LyricAssistModelTests : LyricEditorTestContext
{
    [Fact]
    public void ClaudeNotice_ShowsTheServerText_AndCancelSendsNothing()
    {
        Api.GetLyricAssistStatusAsync(Arg.Any<CancellationToken>()).Returns(Status(AgentBackendDto.Claude, notice: true));
        var page = Open(ExperienceLevel.Beginner);

        page.Find(Id("lyric-assist-check")).Click();

        var dialog = page.WaitForElement(Id("lyric-assist-claude-notice"));
        (dialog.GetAttribute("role"), dialog.GetAttribute("aria-modal")).ShouldBe(("alertdialog", "true"));
        dialog.TextContent.ShouldContain("Your lyrics text is sent to Anthropic for this check; paid per use.");
        page.Find(Id("lyric-assist-claude-notice-cancel")).Click();

        page.FindAll(Id("lyric-assist-claude-notice")).ShouldBeEmpty();
        Api.DidNotReceive().CreateLyricAssistAsync(Arg.Any<Guid>(), Arg.Any<CreateLyricAssistRequest>(), Arg.Any<CancellationToken>());
        Api.DidNotReceive().UpdateLyricAssistSettingsAsync(Arg.Any<LyricAssistSettingsUpdate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ClaudeNotice_Escape_Cancels()
    {
        Api.GetLyricAssistStatusAsync(Arg.Any<CancellationToken>()).Returns(Status(AgentBackendDto.Claude, notice: true));
        var page = Open(ExperienceLevel.Beginner);
        page.Find(Id("lyric-assist-check")).Click();

        page.WaitForElement(Id("lyric-assist-claude-notice")).KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        page.WaitForAssertion(() => page.FindAll(Id("lyric-assist-claude-notice")).ShouldBeEmpty());
    }

    [Fact]
    public void ClaudeNotice_Acknowledged_RunsTheRequest()
    {
        Api.GetLyricAssistStatusAsync(Arg.Any<CancellationToken>()).Returns(Status(AgentBackendDto.Claude, notice: true));
        Api.UpdateLyricAssistSettingsAsync(Arg.Any<LyricAssistSettingsUpdate>(), Arg.Any<CancellationToken>()).Returns(LyricAssistData.Settings() with { ClaudeNoticeAcknowledged = true });
        var page = Open(ExperienceLevel.Beginner);
        page.Find(Id("lyric-assist-devices")).Click();

        page.WaitForElement(Id("lyric-assist-claude-notice-ack")).Click();

        page.WaitForAssertion(() => Api.Received(1).CreateLyricAssistAsync(LyricsId, Arg.Is<CreateLyricAssistRequest>(r => r.Kind == AssistKindDto.Devices), Arg.Any<CancellationToken>()));
        Received.InOrder(() =>
        {
            Api.UpdateLyricAssistSettingsAsync(Arg.Is<LyricAssistSettingsUpdate>(u => u.AcknowledgeClaudeNotice), Arg.Any<CancellationToken>());
            Api.CreateLyricAssistAsync(LyricsId, Arg.Any<CreateLyricAssistRequest>(), Arg.Any<CancellationToken>());
        });
        page.FindAll(Id("lyric-assist-claude-notice")).ShouldBeEmpty();
    }

    [Fact]
    public void Fallback_IsNamed_WithTheEstimate()
    {
        Stream((LyricAssistEventTypes.Started, new LyricAssistStartedEventDto
        {
            AssistId = AssistId,
            Layers = [],
            Backend = "ollama",
            Model = "qwen3.5:4b",
            UsingFallback = true,
            BlocksTotal = 5,
            EstimatedSeconds = 119.2,
        }));
        Api.GetLyricAssistAsync(AssistId, Arg.Any<CancellationToken>()).Returns(Assist(AssistStatusDto.Running) with { Model = "qwen3.5:4b", UsingFallback = true, EstimatedSeconds = 119.2 });
        var page = Open(ExperienceLevel.Advanced);

        page.Find(Id("lyric-assist-check")).Click();

        page.WaitForAssertion(() =>
        {
            var layers = page.Find(Id("lyric-assist-layers"));
            layers.GetAttribute("data-fallback").ShouldBe("true");
            layers.TextContent.ShouldBe("Spelling: OpenTaal dictionary - grammar and flow: qwen3.5:4b, local (smaller model on the processor while a heavy job runs, about 120 s)");
        });
    }

    [Fact]
    public void FallbackStatus_WithoutAnEstimate_IsNamed()
    {
        Api.GetLyricAssistStatusAsync(Arg.Any<CancellationToken>()).Returns(Status() with { Model = Status().Model with { UsingFallback = true } });

        var page = Open(ExperienceLevel.Advanced);

        page.Find(Id("lyric-assist-layers")).TextContent.ShouldEndWith("(smaller model on the processor while a heavy job runs)");
    }

    [Fact]
    public void Claude_IsNamedOnline_AndUsageIsShownFromAdvanced()
    {
        Api.GetLyricAssistStatusAsync(Arg.Any<CancellationToken>()).Returns(Status(AgentBackendDto.Claude) with { Model = Status().Model with { Backend = AgentBackendDto.Claude, ActiveModel = "claude-model" } });
        Stream((LyricAssistEventTypes.UsageUpdated, new TokenUsageDto { InputTokens = 1200, OutputTokens = 300, CachedInputTokens = 0 }), (LyricAssistEventTypes.Completed, Completed()));

        var advanced = Ran(ExperienceLevel.Advanced);
        advanced.Find(Id("lyric-assist-layers")).TextContent.ShouldContain(", Claude, online");
        advanced.Find(".ss-assist-usage").TextContent.ShouldBe("Usage of the last assist: 1200 input and 300 output tokens");

        var beginner = Ran(ExperienceLevel.Beginner);
        beginner.FindAll(".ss-assist-usage").ShouldBeEmpty();
    }

    private IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<ExperienceContext>> Ran(ExperienceLevel level)
    {
        var page = Open(level);
        page.Find(Id("lyric-assist-check")).Click();
        page.WaitForAssertion(() => page.Find(Id("lyric-assist-pane")).GetAttribute("data-state").ShouldBe("done"));
        return page;
    }

    [Fact]
    public async Task AssistLog_ListsTheNewestAssists_ForExperts()
    {
        Api.ListLyricAssistsAsync(LyricsId, Arg.Any<CancellationToken>()).Returns([
            Assist(AssistStatusDto.Completed) with
            {
                BlocksDone = 4, BlocksTotal = 5, BlocksSkipped = 1, Discarded = DiscardedCountsDto.None with { InvalidAnchor = 2, Verbatim = 1 },
                Usage = new TokenUsageDto { InputTokens = 10, OutputTokens = 5, CachedInputTokens = 0 },
            },
            Assist(AssistStatusDto.Failed, AssistKindDto.Titles) with { AssistId = Guid.NewGuid(), Model = null, Usage = null },
        ]);
        var page = Open(ExperienceLevel.Expert);

        await page.Find(Id("lyric-assist-log")).TriggerEventAsync("ontoggle", EventArgs.Empty);

        var log = page.Find(Id("lyric-assist-log"));
        log.TextContent.ShouldContain("4 / 5 (1 skipped)");
        log.TextContent.ShouldContain("2 anchor, 0 schema, 0 check, 0 filtered, 1 withheld");
        log.TextContent.ShouldContain("10 in / 5 out");
        log.TextContent.ShouldContain("Title ideas");
        log.TextContent.ShouldContain("none");
    }

    [Fact]
    public async Task Revisions_CanBeRestored_ForExperts()
    {
        Api.ListLyricsRevisionsAsync(LyricsId, Arg.Any<CancellationToken>()).Returns([
            new LyricsRevisionSummaryDto { Revision = 3, Cause = RevisionCauseDto.Accept, CreatedAt = DateTimeOffset.UnixEpoch },
            new LyricsRevisionSummaryDto { Revision = 2, Cause = RevisionCauseDto.UserSave, CreatedAt = DateTimeOffset.UnixEpoch },
            new LyricsRevisionSummaryDto { Revision = 1, Cause = RevisionCauseDto.Created, CreatedAt = DateTimeOffset.UnixEpoch },
        ]);
        Api.RestoreLyricsRevisionAsync(LyricsId, 2, 3, Arg.Any<CancellationToken>()).Returns(Accepted("oude tekst", revision: 4));
        var page = Open(ExperienceLevel.Expert);

        await page.Find(Id("lyric-revisions-open")).ClickAsync(new());

        page.FindAll(Id("lyric-revision-restore-3")).ShouldBeEmpty();
        page.Find(Id("lyric-revision-2")).TextContent.ShouldContain("your save");
        page.Find(Id("lyric-revision-1")).TextContent.ShouldContain("created");
        await page.Find(Id("lyric-revision-restore-2")).ClickAsync(new());

        page.Find(Id("lyrics-text-input")).GetAttribute("value").ShouldBe("oude tekst");
        page.Find(Id("lyric-assist-live")).TextContent.ShouldBe("Revision 2 restored");
    }

    [Fact]
    public void FailedAssist_ShowsTheMessage()
    {
        Stream((LyricAssistEventTypes.Failed, new LyricAssistFailedEventDto { Code = "backend-unavailable", Message = "Ollama stopped.", Fix = "Start Ollama." }));

        var page = Open(ExperienceLevel.Beginner);
        page.Find(Id("lyric-assist-check")).Click();

        page.WaitForAssertion(() => page.Find(Id("lyric-assist-live")).TextContent.ShouldBe("Ollama stopped."));
        page.Find(Id("lyric-assist-pane")).GetAttribute("data-state").ShouldBe("done");
    }
}

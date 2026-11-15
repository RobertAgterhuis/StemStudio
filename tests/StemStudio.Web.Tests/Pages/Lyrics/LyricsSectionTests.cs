using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Pages.Lyrics;

/// <summary>The lyrics card of the track page (006 T046, FR-004, FR-010; ui-test-ids 1.6.0).</summary>
public sealed class LyricsSectionTests : LyricsTestContext
{
    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(LyricsStatusDto.None, "none", "Not searched yet")]
    [InlineData(LyricsStatusDto.Searching, "searching", "Searching")]
    [InlineData(LyricsStatusDto.Instrumental, "instrumental", "Instrumental")]
    [InlineData(LyricsStatusDto.NotFound, "notFound", "Not found")]
    [InlineData(LyricsStatusDto.Failed, "failed", "Failed")]
    [InlineData(LyricsStatusDto.Disabled, "disabled", "Online lookups off")]
    [InlineData(LyricsStatusDto.Removed, "removed", "Removed")]
    public void Status_IsShownWithItsWireValue(LyricsStatusDto status, string value, string label)
    {
        var page = RenderSection(LyricsData.Empty(status));

        var section = page.Find("[data-testid=lyrics-section]");
        section.GetAttribute("data-help-id").ShouldBe("lyrics-sources");
        var badge = page.Find("[data-testid=lyrics-status]");
        badge.GetAttribute("data-status").ShouldBe(value);
        badge.TextContent.ShouldContain(label);
        page.FindAll("[data-testid=lyrics-credit]").ShouldBeEmpty();
        page.FindAll("[data-testid=lyrics-preview]").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(SyncLevelDto.Word, LyricsOriginDto.Source, "Found, timed per word")]
    [InlineData(SyncLevelDto.Line, LyricsOriginDto.Interpolated, "Found, timed per line")]
    [InlineData(SyncLevelDto.Word, LyricsOriginDto.Aligned, "Found, aligned to the vocals")]
    public void FoundLyrics_ShowTheirTimingAndCredit(SyncLevelDto sync, LyricsOriginDto origin, string label)
    {
        var page = RenderSection(LyricsData.Found(sync, origin));

        page.Find("[data-testid=lyrics-status]").TextContent.ShouldContain(label);
        page.Find("[data-testid=lyrics-credit]").TextContent.ShouldBe(LyricsData.Found(sync, origin).Credit!);
        page.FindAll("[data-testid=lyrics-preview]").ShouldBeEmpty();
        page.FindAll("[data-testid=lyrics-align]").ShouldBeEmpty();
    }

    [Fact]
    public void CopyrightNotice_IsAlwaysShown()
    {
        var page = RenderSection(LyricsData.Found());

        page.Find("[data-testid=lyrics-copyright-notice]").TextContent.ShouldContain("Lyrics are shown for your personal practice only");
    }

    [Fact]
    public void AlignPending_ShowsThePreviewTheEstimateAndTheAlignButton()
    {
        var page = RenderSection(LyricsData.Plain());

        page.Find("[data-testid=lyrics-preview]").TextContent.ShouldContain("Fake line one");
        page.FindAll("[data-testid=lyrics-preview] p").Select(p => p.TextContent).ShouldBe(["Fake line one", "La la la"]);
        page.Find("[data-testid=lyrics-align-estimate]").TextContent.ShouldBe("Aligning takes about 7 minutes on the CPU.");
        var align = page.Find("[data-testid=lyrics-align]");
        align.GetAttribute("data-help-id").ShouldBe("lyrics-align");
        align.TextContent.ShouldContain("Align to the vocals");
    }

    [Theory]
    [InlineData(30, "Aligning takes about 30 seconds.")]
    [InlineData(89, "Aligning takes about 89 seconds.")]
    [InlineData(420, "Aligning takes about 7 minutes.")]
    [InlineData(421, "Aligning takes about 8 minutes.")]
    public void Estimate_WithoutAReason_ComesFromTheSeconds(int seconds, string text)
    {
        var page = RenderSection(LyricsData.Plain(reason: null) with { Alignment = new LyricsAlignmentDto(seconds, null, null) });

        page.Find("[data-testid=lyrics-align-estimate]").TextContent.ShouldBe(text);
    }

    [Fact]
    public async Task Align_StartsTheAlignment_AndPollsItUntilFound()
    {
        Api.AlignLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(LyricsData.Aligning(0));
        var page = RenderSection(LyricsData.Plain(), LyricsData.Aligning(55), LyricsData.Found(origin: LyricsOriginDto.Aligned));

        await page.Find("[data-testid=lyrics-align]").ClickAsync(new());

        page.Find("[data-testid=lyrics-status]").GetAttribute("data-status").ShouldBe("aligning");
        Timer.Tick();
        page.WaitForAssertion(() => page.Find("[data-testid=lyrics-align-progress]").GetAttribute("aria-valuenow").ShouldBe("55"));
        Timer.Tick();
        page.WaitForAssertion(() => page.Find("[data-testid=lyrics-status]").GetAttribute("data-status").ShouldBe("found"));
        page.FindAll("[data-testid=lyrics-align-progress]").ShouldBeEmpty();
    }

    [Fact]
    public void Aligning_ShowsProgressEstimateDeviceAndCancel()
    {
        var page = RenderSection(LyricsData.Aligning(40));

        page.Find("[data-testid=lyrics-align-progress]").GetAttribute("aria-valuenow").ShouldBe("40");
        page.Find("[data-testid=lyrics-align-estimate]").TextContent.ShouldBe("Aligning takes about 30 seconds.");
        var device = page.Find("[data-testid=lyrics-align-device]");
        (device.GetAttribute("data-device"), device.TextContent).ShouldBe(("gpu", "GPU (NVIDIA GeForce RTX 4070)"));
        page.Find("[data-testid=lyrics-align-cancel]").ShouldNotBeNull();
        page.FindAll("[data-testid=lyrics-align]").ShouldBeEmpty();
        page.FindAll("[data-testid=lyrics-paste-open]").ShouldBeEmpty();
        Timer.Waits.ShouldBe(1);
    }

    [Fact]
    public void Aligning_WithoutDeviceOrEstimate_ShowsTheProgressOnly()
    {
        foreach (var alignment in new LyricsAlignmentDto?[] { new(null, null, null), null })
        {
            var page = RenderSection(LyricsData.Aligning(10) with { Alignment = alignment });

            page.Find("[data-testid=lyrics-align-progress]").GetAttribute("aria-valuenow").ShouldBe("0");
            page.FindAll("[data-testid=lyrics-align-device]").ShouldBeEmpty();
            page.FindAll("[data-testid=lyrics-align-estimate]").ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task Cancel_StopsTheAlignment()
    {
        Api.CancelLyricsAlignmentAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(LyricsData.Plain());
        var page = RenderSection(LyricsData.Aligning(40));

        await page.Find("[data-testid=lyrics-align-cancel]").ClickAsync(new());

        await Api.Received(1).CancelLyricsAlignmentAsync(TestData.TrackId, Arg.Any<CancellationToken>());
        page.Find("[data-testid=lyrics-status]").GetAttribute("data-status").ShouldBe("alignPending");
    }

    [Fact]
    public void AlignDevice_IsHiddenAtBeginner()
    {
        var page = RenderAt(ExperienceLevel.Beginner, LyricsData.Aligning(40));

        page.FindAll("[data-testid=lyrics-align-device]").ShouldBeEmpty();
        page.FindAll("[data-testid=lyrics-align-progress]").Count.ShouldBe(1);
    }

    [Fact]
    public void FailedAlignment_ShowsTheReasonThePreviewTheSplitHintAndTryAgain()
    {
        var page = RenderSection(LyricsData.Plain(LyricsStatusDto.Failed, "The alignment did not find the words in the vocals."));

        page.Find("[data-testid=lyrics-error]").TextContent.ShouldBe("The alignment did not find the words in the vocals.");
        page.Find("[data-testid=lyrics-preview]").TextContent.ShouldContain("La la la");
        page.Find(".ss-lyrics-split-hint").TextContent.ShouldBe("Split the vocals into detail stems and try again.");
        page.Find("[data-testid=lyrics-align]").TextContent.ShouldContain("Try again");
    }

    [Fact]
    public void FailedAlignment_OfSplitVocals_HasNoSplitHint()
    {
        var page = RenderSection(TestData.DetailTrack(), LyricsData.Plain(LyricsStatusDto.Failed, "Failed."));

        page.FindAll(".ss-lyrics-split-hint").ShouldBeEmpty();
    }

    [Fact]
    public void FailedLookup_ShowsTheReasonOnly()
    {
        var page = RenderSection(LyricsData.Empty(LyricsStatusDto.Failed, "Lyrics could not be retrieved; try again later."));

        page.Find("[data-testid=lyrics-error]").TextContent.ShouldBe("Lyrics could not be retrieved; try again later.");
        page.FindAll("[data-testid=lyrics-preview]").ShouldBeEmpty();
        page.FindAll("[data-testid=lyrics-align]").ShouldBeEmpty();
        page.FindAll(".ss-lyrics-split-hint").ShouldBeEmpty();
    }

    [Fact]
    public void NotFound_ShowsTheReasonAsHelp_NotAsError()
    {
        var page = RenderSection(LyricsData.Empty(LyricsStatusDto.NotFound, "No lyrics were found online. You can enter them yourself."));

        page.FindAll("[data-testid=lyrics-error]").ShouldBeEmpty();
        page.Find(".ss-lyrics-reason").TextContent.ShouldBe("No lyrics were found online. You can enter them yourself.");
        page.Find("[data-testid=lyrics-paste-open]").ShouldNotBeNull();
    }

    [Fact]
    public async Task AlignConflict_IsShownAsError()
    {
        Api.AlignLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.Conflict, "The vocal stem is no longer available."));
        var page = RenderSection(LyricsData.Plain());

        await page.Find("[data-testid=lyrics-align]").ClickAsync(new());

        page.Find("[data-testid=lyrics-error]").TextContent.ShouldBe("The vocal stem is no longer available.");
    }

    [Fact]
    public void LoadError_IsShown()
    {
        Api.GetLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "Down"));

        var page = Render<StemStudio.Web.Components.Lyrics.LyricsSection>(p => p.Add(c => c.Track, TestData.Track()));

        page.Find("[data-testid=lyrics-error]").TextContent.ShouldBe("Down");
        page.FindAll("[data-testid=lyrics-status]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Paste_OpensATextArea_AndSubmitsTheText()
    {
        Api.SetLyricsAsync(TestData.TrackId, Arg.Any<SetLyricsRequest>(), Arg.Any<CancellationToken>()).Returns(LyricsData.Aligning(0));
        var page = RenderSection(LyricsData.Empty(LyricsStatusDto.NotFound));

        await page.Find("[data-testid=lyrics-paste-open]").ClickAsync(new());
        page.Find("[data-testid=lyrics-paste-input]").TagName.ShouldBe("TEXTAREA");
        await page.Find("[data-testid=lyrics-paste-input]").InputAsync(new() { Value = "My own line\nSecond line" });
        await page.Find("[data-testid=lyrics-paste-submit]").ClickAsync(new());

        await Api.Received(1).SetLyricsAsync(TestData.TrackId, new SetLyricsRequest("My own line\nSecond line"), Arg.Any<CancellationToken>());
        page.FindAll("[data-testid=lyrics-paste-input]").ShouldBeEmpty();
        page.Find("[data-testid=lyrics-status]").GetAttribute("data-status").ShouldBe("aligning");
        Timer.Waits.ShouldBe(1);
    }

    [Theory]
    [InlineData("   ", "Enter the lyrics")]
    [InlineData("bad\u0007text", "control characters")]
    public async Task Paste_IsCheckedBeforeSubmit(string text, string message)
    {
        var page = RenderSection(LyricsData.Empty(LyricsStatusDto.NotFound));

        await page.Find("[data-testid=lyrics-paste-open]").ClickAsync(new());
        await page.Find("[data-testid=lyrics-paste-input]").InputAsync(new() { Value = text });
        await page.Find("[data-testid=lyrics-paste-submit]").ClickAsync(new());

        page.Find("[data-testid=lyrics-paste-error]").TextContent.ShouldContain(message);
        await Api.DidNotReceiveWithAnyArgs().SetLyricsAsync(default, default!, Ct);
    }

    [Fact]
    public async Task Paste_LongerThanTheLimit_IsRefused()
    {
        var page = RenderSection(LyricsData.Empty(LyricsStatusDto.NotFound));

        await page.Find("[data-testid=lyrics-paste-open]").ClickAsync(new());
        page.Find("[data-testid=lyrics-paste-input]").GetAttribute("maxlength").ShouldBe("20000");
        await page.Find("[data-testid=lyrics-paste-input]").InputAsync(new() { Value = new string('a', 20_001) });
        await page.Find("[data-testid=lyrics-paste-submit]").ClickAsync(new());

        page.Find("[data-testid=lyrics-paste-error]").TextContent.ShouldContain("20000 characters");
    }

    [Fact]
    public async Task Paste_ServerRefusal_KeepsTheTextAndShowsTheReason()
    {
        Api.SetLyricsAsync(TestData.TrackId, Arg.Any<SetLyricsRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.UnprocessableEntity, "A line has more than 40 words."));
        var page = RenderSection(LyricsData.Empty(LyricsStatusDto.NotFound));

        await page.Find("[data-testid=lyrics-paste-open]").ClickAsync(new());
        await page.Find("[data-testid=lyrics-paste-input]").InputAsync(new() { Value = "Some text" });
        await page.Find("[data-testid=lyrics-paste-submit]").ClickAsync(new());

        page.Find("[data-testid=lyrics-paste-error]").TextContent.ShouldBe("A line has more than 40 words.");
        page.Find("[data-testid=lyrics-paste-input]").GetAttribute("value").ShouldBe("Some text");
    }

    [Fact]
    public async Task Paste_CanBeCancelled()
    {
        var page = RenderSection(LyricsData.Empty(LyricsStatusDto.NotFound));

        await page.Find("[data-testid=lyrics-paste-open]").ClickAsync(new());
        await page.Find(".ss-lyrics-paste-cancel").ClickAsync(new());

        page.FindAll("[data-testid=lyrics-paste-input]").ShouldBeEmpty();
    }

    [Fact]
    public void Paste_IsNotOfferedWhileSearching() =>
        RenderSection(LyricsData.Empty(LyricsStatusDto.Searching)).FindAll("[data-testid=lyrics-paste-open]").ShouldBeEmpty();

    [Fact]
    public void Expert_CanRefineLineTimedLyrics()
    {
        RenderAt(ExperienceLevel.Expert, LyricsData.Found(SyncLevelDto.Line, LyricsOriginDto.Interpolated))
            .Find("[data-testid=lyrics-align]").TextContent.ShouldContain("Align to the vocals");
        RenderAt(ExperienceLevel.Advanced, LyricsData.Found(SyncLevelDto.Line, LyricsOriginDto.Interpolated))
            .FindAll("[data-testid=lyrics-align]").ShouldBeEmpty();
        RenderAt(ExperienceLevel.Expert, LyricsData.Found())
            .FindAll("[data-testid=lyrics-align]").ShouldBeEmpty();
    }

    [Fact]
    public void Searching_IsPolledUntilSettled()
    {
        var page = RenderSection(LyricsData.Empty(LyricsStatusDto.Searching), LyricsData.Empty(LyricsStatusDto.NotFound));

        Timer.Tick();

        page.WaitForAssertion(() => page.Find("[data-testid=lyrics-status]").GetAttribute("data-status").ShouldBe("notFound"));
    }
}

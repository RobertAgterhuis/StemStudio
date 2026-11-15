using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Audio;
using StemStudio.Web.Components.Mixer.Karaoke;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer.Karaoke;

/// <summary>The karaoke banner in the mixer (006 T037, FR-005, FR-007, FR-015; ui-test-ids 1.6.0).</summary>
public sealed class KaraokeBannerTests : KaraokeTestContext
{
    [Fact]
    public void FoundLyrics_RenderAnEmptyEngineOwnedHost_WithSyncCreditAndHelpMarker()
    {
        var page = RenderBanner(LyricsData.Found());

        var host = page.Find("[data-testid=karaoke-banner]");
        host.Id.ShouldBe("karaoke-host");
        host.ChildElementCount.ShouldBe(0);
        host.TextContent.ShouldBeEmpty();
        host.GetAttribute("data-sync").ShouldBe("word");
        host.GetAttribute("data-help-id").ShouldBe("karaoke");
        host.GetAttribute("aria-live").ShouldBe("off");
        page.Find("[data-testid=karaoke-credit]").TextContent.ShouldBe("Lyrics: LRCLIB");
        page.FindAll("[data-testid=karaoke-unavailable]").ShouldBeEmpty();
    }

    [Fact]
    public void WhileTheLyricsLoad_NeitherTheBannerNorTheNoticeIsRendered()
    {
        var pending = new TaskCompletionSource<LyricsDto>();
        Api.GetLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(pending.Task);

        var page = Render<KaraokeBanner>(p => p.Add(c => c.TrackId, TestData.TrackId).Add(c => c.EngineReady, true));

        page.FindAll("[data-testid=karaoke-unavailable]").ShouldBeEmpty();
        page.FindAll("[data-testid=karaoke-banner]").ShouldBeEmpty();
        page.Find("[data-testid=karaoke-toggle]");
        pending.SetResult(LyricsData.Found());
        page.WaitForAssertion(() => page.Find("[data-testid=karaoke-banner]").GetAttribute("data-sync").ShouldBe("word"));
    }

    [Theory]
    [InlineData(SyncLevelDto.Word, LyricsOriginDto.Source, "word")]
    [InlineData(SyncLevelDto.Line, LyricsOriginDto.Interpolated, "line")]
    [InlineData(SyncLevelDto.Word, LyricsOriginDto.Aligned, "aligned")]
    public void DataSync_FollowsTheTimingOfTheDocument(SyncLevelDto sync, LyricsOriginDto origin, string expected)
    {
        var page = RenderBanner(LyricsData.Found(sync, origin));

        page.Find("[data-testid=karaoke-banner]").GetAttribute("data-sync").ShouldBe(expected);
    }

    [Fact]
    public void Attach_WaitsUntilTheTrackIsLoadedIntoTheEngine()
    {
        var page = RenderBanner(false, LyricsData.Found(offsetMs: 120));
        Engine.DidNotReceiveWithAnyArgs().AttachKaraokeAsync(default!, default!, default!);

        page.Render(p => p.Add(c => c.EngineReady, true));

        page.WaitForAssertion(() => Engine.Received(1).AttachKaraokeAsync("karaoke-host", Arg.Any<KaraokeLyrics>(), Arg.Any<KaraokeOptions>()));
        AttachedOptions().OffsetMs.ShouldBe(120);
        AttachedOptions().ReducedMotion.ShouldBeNull();
    }

    [Fact]
    public void AttachedLyrics_AreTheTimedLinesAndWords()
    {
        RenderBanner(LyricsData.Found());

        var lyrics = AttachedLyrics();
        lyrics.Lines.Count.ShouldBe(2);
        lyrics.Lines[0].ShouldBe(lyrics.Lines[0] with { StartMs = 10950, EndMs = 13200 });
        lyrics.Lines[1].Words.Select(w => (w.Text, w.StartMs, w.EndMs)).ShouldBe([("La", 14000, 14500), ("la", 14500, 15000), ("la", 15000, 16000)]);
    }

    [Fact]
    public async Task Dispose_DetachesTheBanner()
    {
        RenderBanner(LyricsData.Found());

        await DisposeComponentsAsync();

        await Engine.Received(1).DetachKaraokeAsync();
    }

    [Fact]
    public async Task Dispose_SurvivesAnEngineThatIsAlreadyGone()
    {
        Engine.DetachKaraokeAsync().ThrowsAsync(new InvalidOperationException("The audio engine is not initialized."));
        RenderBanner(LyricsData.Found());

        await Should.NotThrowAsync(() => DisposeComponentsAsync());
    }

    [Fact]
    public void OffsetSlider_HasTheContractRange_AndShowsTheStoredOffset()
    {
        var slider = RenderBanner(LyricsData.Found(offsetMs: -250)).Find("[data-testid=karaoke-offset]");

        (slider.GetAttribute("type"), slider.GetAttribute("min"), slider.GetAttribute("max"), slider.GetAttribute("step"))
            .ShouldBe(("range", "-5000", "5000", "10"));
        slider.GetAttribute("aria-valuenow").ShouldBe("-250");
        slider.GetAttribute("aria-valuetext").ShouldBe("-250 ms");
        slider.GetAttribute("data-help-id").ShouldBe("karaoke-offset");
    }

    [Fact]
    public async Task OffsetChange_AppliesAtOnce_AndIsSavedDebounced()
    {
        var page = RenderBanner(LyricsData.Found());
        var slider = page.Find("[data-testid=karaoke-offset]");

        await slider.InputAsync(new() { Value = "200" });
        await page.Find("[data-testid=karaoke-offset]").InputAsync(new() { Value = "250" });

        await Engine.Received(1).SetKaraokeOffsetAsync(200);
        await Engine.Received(1).SetKaraokeOffsetAsync(250);
        await Api.DidNotReceiveWithAnyArgs().SetLyricsOffsetAsync(default, default, Xunit.TestContext.Current.CancellationToken);
        Time.Advance(TimeSpan.FromMilliseconds(499));
        await Api.DidNotReceiveWithAnyArgs().SetLyricsOffsetAsync(default, default, Xunit.TestContext.Current.CancellationToken);
        Time.Advance(TimeSpan.FromMilliseconds(1));
        page.WaitForAssertion(() => Api.Received(1).SetLyricsOffsetAsync(TestData.TrackId, 250, Arg.Any<CancellationToken>()));
        page.Find("[data-testid=karaoke-offset]").GetAttribute("aria-valuenow").ShouldBe("250");
    }

    [Theory]
    [InlineData("1234", 1230)]
    [InlineData("9000", 5000)]
    [InlineData("-7000", -5000)]
    [InlineData("-15", -20)]
    public async Task OffsetInput_IsSnappedToTenMillisecondsAndClamped(string typed, int expected)
    {
        var page = RenderBanner(LyricsData.Found());

        await page.Find("[data-testid=karaoke-offset]").InputAsync(new() { Value = typed });

        await Engine.Received(1).SetKaraokeOffsetAsync(expected);
    }

    [Fact]
    public async Task UnreadableOffsetInput_IsIgnored()
    {
        var page = RenderBanner(LyricsData.Found());

        await page.Find("[data-testid=karaoke-offset]").InputAsync(new() { Value = "abc" });

        await Engine.DidNotReceiveWithAnyArgs().SetKaraokeOffsetAsync(default);
    }

    [Theory]
    [InlineData("ArrowRight", 100, 150)]
    [InlineData("ArrowUp", 100, 150)]
    [InlineData("ArrowLeft", 100, 50)]
    [InlineData("ArrowDown", 100, 50)]
    [InlineData("ArrowRight", 4990, 5000)]
    public async Task ArrowKeys_MoveTheOffsetByFiftyMilliseconds(string key, int start, int expected)
    {
        var page = RenderBanner(LyricsData.Found(offsetMs: start));
        var nativeStep = key is "ArrowRight" or "ArrowUp" ? 10 : -10;

        await page.Find("[data-testid=karaoke-offset]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = key });
        await page.Find("[data-testid=karaoke-offset]").InputAsync(new() { Value = (start + nativeStep).ToString(System.Globalization.CultureInfo.InvariantCulture) });

        await Engine.Received(1).SetKaraokeOffsetAsync(expected);
        page.Find("[data-testid=karaoke-offset]").GetAttribute("aria-valuenow").ShouldBe(expected.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task OtherKeys_DoNotChangeTheNativeStep()
    {
        var page = RenderBanner(LyricsData.Found(offsetMs: 100));

        await page.Find("[data-testid=karaoke-offset]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Home" });
        await page.Find("[data-testid=karaoke-offset]").InputAsync(new() { Value = "110" });

        await Engine.Received(1).SetKaraokeOffsetAsync(110);
    }

    [Fact]
    public async Task Reset_SetsTheOffsetToZero()
    {
        var page = RenderBanner(LyricsData.Found(offsetMs: 300));

        await page.Find("[data-testid=karaoke-offset-reset]").ClickAsync(new());

        await Engine.Received(1).SetKaraokeOffsetAsync(0);
        Time.Advance(TimeSpan.FromMilliseconds(500));
        page.WaitForAssertion(() => Api.Received(1).SetLyricsOffsetAsync(TestData.TrackId, 0, Arg.Any<CancellationToken>()));
    }

    [Fact]
    public async Task FailedSave_IsShown()
    {
        Api.SetLyricsOffsetAsync(TestData.TrackId, 250, Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(System.Net.HttpStatusCode.Conflict, "The track has no lyrics."));
        var page = RenderBanner(LyricsData.Found());

        await page.Find("[data-testid=karaoke-offset]").InputAsync(new() { Value = "250" });
        Time.Advance(TimeSpan.FromMilliseconds(500));

        page.WaitForAssertion(() => page.Find(".ss-karaoke-error").TextContent.ShouldContain("The track has no lyrics."));
    }

    [Theory]
    [InlineData(LyricsStatusDto.Searching, "searching", "Searching for the lyrics")]
    [InlineData(LyricsStatusDto.None, "none", "Searching for the lyrics")]
    [InlineData(LyricsStatusDto.Instrumental, "instrumental", "instrumental")]
    [InlineData(LyricsStatusDto.NotFound, "notFound", "No lyrics were found online")]
    [InlineData(LyricsStatusDto.Disabled, "disabled", "Online lookups are switched off")]
    [InlineData(LyricsStatusDto.Removed, "removed", "You removed the lyrics")]
    [InlineData(LyricsStatusDto.Failed, "failed", "The lyrics could not be retrieved")]
    public void WithoutTimedLyrics_TheUnavailableNoticeReplacesTheHost(LyricsStatusDto status, string value, string text)
    {
        var page = RenderBanner(LyricsData.Empty(status));

        page.FindAll("[data-testid=karaoke-banner]").ShouldBeEmpty();
        var notice = page.Find("[data-testid=karaoke-unavailable]");
        notice.GetAttribute("data-status").ShouldBe(value);
        notice.TextContent.ShouldContain(text);
        notice.QuerySelector("a")!.GetAttribute("href").ShouldBe($"tracks/{TestData.TrackId}");
        page.FindAll("[data-testid=karaoke-offset]").ShouldBeEmpty();
        Engine.DidNotReceiveWithAnyArgs().AttachKaraokeAsync(default!, default!, default!);
    }

    [Fact]
    public void UnavailableNotice_ShowsTheReason_AndTheAlignmentProgress()
    {
        RenderBanner(LyricsData.Empty(LyricsStatusDto.Failed, "Lyrics could not be retrieved; try again later."))
            .Find("[data-testid=karaoke-unavailable]").TextContent.ShouldContain("Lyrics could not be retrieved; try again later.");
        RenderBanner(LyricsData.Aligning(40))
            .Find("[data-testid=karaoke-unavailable]").TextContent.ShouldContain("being aligned to the vocals (40 %)");
        RenderBanner(LyricsData.Plain())
            .Find("[data-testid=karaoke-unavailable]").TextContent.ShouldContain("no timing yet");
    }

    [Fact]
    public void FoundDocumentWithoutTimes_IsNotAttached()
    {
        var page = RenderBanner(LyricsData.Plain(LyricsStatusDto.Found, null));

        page.Find("[data-testid=karaoke-unavailable]").GetAttribute("data-status").ShouldBe("found");
        Engine.DidNotReceiveWithAnyArgs().AttachKaraokeAsync(default!, default!, default!);
    }

    [Fact]
    public void SearchingLyrics_ArePolledUntilFound_ThenAttached()
    {
        var page = RenderBanner(LyricsData.Empty(LyricsStatusDto.Searching), LyricsData.Found());

        Timer.Tick();

        page.WaitForAssertion(() => page.Find("[data-testid=karaoke-banner]").ShouldNotBeNull());
        page.WaitForAssertion(() => Engine.Received(1).AttachKaraokeAsync("karaoke-host", Arg.Any<KaraokeLyrics>(), Arg.Any<KaraokeOptions>()));
        Timer.Tick();
        page.WaitForAssertion(() => Api.Received(2).GetLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()));
    }

    [Fact]
    public void FoundLyrics_AreNotPolled()
    {
        RenderBanner(LyricsData.Found());

        Timer.Waits.ShouldBe(0);
    }

    [Fact]
    public void LoadError_IsShownAsUnavailable()
    {
        Api.GetLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).ThrowsAsync(Problem(System.Net.HttpStatusCode.ServiceUnavailable, "Down"));

        var page = Render<StemStudio.Web.Components.Mixer.Karaoke.KaraokeBanner>(p => p.Add(c => c.TrackId, TestData.TrackId).Add(c => c.EngineReady, true));

        page.Find("[data-testid=karaoke-unavailable]").TextContent.ShouldContain("Down");
    }

    [Fact]
    public void Beginner_SeesTheBannerAndTheToggleOnly()
    {
        var page = RenderAt(ExperienceLevel.Beginner, LyricsData.Found());

        page.FindAll("[data-testid=karaoke-banner]").Count.ShouldBe(1);
        page.FindAll("[data-testid=karaoke-credit]").Count.ShouldBe(1);
        page.FindAll("[data-testid=karaoke-toggle]").Count.ShouldBe(1);
        foreach (var id in new[] { "karaoke-size", "karaoke-offset", "karaoke-offset-reset" })
        {
            page.FindAll($"[data-testid={id}]").ShouldBeEmpty(id);
        }

        page.FindAll(".ss-karaoke-origin").ShouldBeEmpty();
    }

    [Fact]
    public void Advanced_AddsOffsetAndSize()
    {
        var page = RenderAt(ExperienceLevel.Advanced, LyricsData.Found());

        foreach (var id in new[] { "karaoke-size", "karaoke-offset", "karaoke-offset-reset", "karaoke-toggle" })
        {
            page.FindAll($"[data-testid={id}]").Count.ShouldBe(1, id);
        }

        page.FindAll(".ss-karaoke-origin").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(LyricsOriginDto.Source, "Timing: from the lyrics source")]
    [InlineData(LyricsOriginDto.Interpolated, "Timing: estimated from the line timing")]
    [InlineData(LyricsOriginDto.Aligned, "Timing: automatic alignment")]
    public void Expert_AlsoSeesTheTimingOrigin(LyricsOriginDto origin, string text)
    {
        var page = RenderAt(ExperienceLevel.Expert, LyricsData.Found(origin: origin));

        page.Find(".ss-karaoke-origin").TextContent.ShouldBe(text);
    }

    [Fact]
    public async Task Dispose_StoresAnOffsetThatWasStillWaiting()
    {
        var page = RenderBanner(LyricsData.Found());
        await page.Find("[data-testid=karaoke-offset]").InputAsync(new() { Value = "70" });

        await DisposeComponentsAsync();

        await Api.Received(1).SetLyricsOffsetAsync(TestData.TrackId, 70, CancellationToken.None);
    }

    [Fact]
    public void AttachFailure_IsShown_AndKeepsTheMixerRunning()
    {
        Engine.AttachKaraokeAsync(Arg.Any<string>(), Arg.Any<KaraokeLyrics>(), Arg.Any<KaraokeOptions>())
            .ThrowsAsync(new AudioEngineException("Invalid karaoke lyrics: word outside its line"));

        var page = RenderBanner(LyricsData.Found());

        page.WaitForAssertion(() => page.Find(".ss-karaoke-error").TextContent.ShouldContain("Invalid karaoke lyrics"));
    }

    [Fact]
    public async Task OffsetChange_BeforeTheEngineIsReady_IsUsedByTheAttach()
    {
        var page = RenderBanner(false, LyricsData.Found());

        await page.Find("[data-testid=karaoke-offset]").InputAsync(new() { Value = "40" });
        page.Render(p => p.Add(c => c.EngineReady, true));

        await Engine.DidNotReceiveWithAnyArgs().SetKaraokeOffsetAsync(default);
        page.WaitForAssertion(() => AttachedOptions().OffsetMs.ShouldBe(40));
    }
}

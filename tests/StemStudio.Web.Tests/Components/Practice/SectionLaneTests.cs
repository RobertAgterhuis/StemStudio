using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Sections;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Practice;
using StemStudio.Web.Experience;
using StemStudio.Web.Interop;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Mixer;
using MixerPage = StemStudio.Web.Components.Mixer.Mixer;

namespace StemStudio.Web.Tests.Components.Practice;

/// <summary>T058: the section lane and the section commands in the mixer (014 US1; FR-004, FR-005, FR-007; ui-test-ids 1.13.0).</summary>
public sealed class SectionLaneTests : MixerTestContext
{
    private static readonly Guid Verse = PracticeFlowData.SectionIds[1];
    private static readonly Guid Solo = PracticeFlowData.SectionIds[3];

    public SectionLaneTests() =>
        Api.GetSectionsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.SectionSet());

    private IRenderedComponent<MixerPage> RenderReady()
    {
        var page = RenderMixer();
        page.WaitForAssertion(() => page.FindAll("[data-testid^=section-0b1c]").Count.ShouldBe(5));
        return page;
    }

    private void Position(double seconds) => Engine.PositionChanged += Raise.Event<EventHandler<double>>(Engine, seconds);

    private static string Id(Guid section) => $"[data-testid=section-{section}]";

    [Fact]
    public void Lane_ListsTheSectionsWithKindLabelBarsAndTestIds()
    {
        var page = RenderReady();

        var lane = page.Find("[data-testid=section-lane]");
        (lane.GetAttribute("role"), lane.GetAttribute("data-status"), lane.GetAttribute("data-revision")).ShouldBe(("list", "ready", "1"));
        var solo = page.Find(Id(Solo));
        solo.TagName.ShouldBe("BUTTON");
        (solo.GetAttribute("data-kind"), solo.GetAttribute("data-start"), solo.GetAttribute("data-end")).ShouldBe(("solo", "40.1", "56.1"));
        solo.TextContent.ShouldStartWith("Guitar solo");
        solo.TextContent.ShouldContain("solo, guitar");
        solo.TextContent.ShouldContain("bars 21 to 28");
        page.Find(Id(PracticeFlowData.SectionIds[2])).GetAttribute("data-kind").ShouldBe("chorus");
        page.Find("[data-testid=section-status]").TextContent.ShouldBe("Detected automatically - check and edit");
    }

    [Fact]
    public void Status_SaysWhenTheGridIsUncertain()
    {
        Api.GetSectionsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.SectionSet() with
        {
            Warnings = [new SectionWarningDto { Code = SectionWarningCodeDto.GridUnreliable, Message = "Grid uncertain" }],
        });

        var page = RenderMixer();

        page.WaitForAssertion(() => page.Find("[data-testid=section-status]").TextContent.ShouldBe("Tempo grid uncertain - boundaries may be off"));
    }

    [Fact]
    public void FirstOpenWithStatusNone_DetectsOnce_AndPollsUntilReady()
    {
        Api.GetSectionsAsync(TestData.TrackId, Arg.Any<CancellationToken>())
            .Returns(PracticeFlowData.SectionSet(SectionSetStatusDto.None), PracticeFlowData.SectionSet(SectionSetStatusDto.Detecting), PracticeFlowData.SectionSet());
        Api.DetectSectionsAsync(TestData.TrackId, false, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.SectionSet(SectionSetStatusDto.Queued));

        var page = RenderMixer();

        page.WaitForAssertion(() => page.Find("[data-testid=section-lane]").GetAttribute("data-status").ShouldBe("queued"));
        page.Find("[data-testid=section-status]").TextContent.ShouldBe("Detecting sections...");
        Timer.Tick();
        page.WaitForAssertion(() => page.Find("[data-testid=section-lane]").GetAttribute("data-status").ShouldBe("detecting"));
        Timer.Tick();
        page.WaitForAssertion(() => page.FindAll("[data-testid^=section-0b1c]").Count.ShouldBe(5));
        Api.Received(1).DetectSectionsAsync(TestData.TrackId, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Failed_ShowsTheReason_AndTryAgainDetects()
    {
        Api.GetSectionsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.SectionSet(SectionSetStatusDto.Failed));
        Api.DetectSectionsAsync(TestData.TrackId, false, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.SectionSet(SectionSetStatusDto.Queued));
        var page = RenderMixer();
        page.WaitForAssertion(() => page.Find("[data-testid=section-status]").TextContent.ShouldBe("The analysis could not read the stems."));

        page.Find("[data-testid=section-retry]").Click();

        page.WaitForAssertion(() => page.FindAll("[data-testid=section-retry]").ShouldBeEmpty());
        Api.Received(1).DetectSectionsAsync(TestData.TrackId, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void AnApiError_IsShownAndNotThrown()
    {
        Api.GetSectionsAsync(TestData.TrackId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new ApiException(new ApiError(HttpStatusCode.ServiceUnavailable, "Down", "The API is down.", new Dictionary<string, string[]>())));

        var page = RenderMixer();

        page.WaitForAssertion(() => page.Find("[data-testid=section-error]").TextContent.ShouldBe("The API is down."));
        page.Find("[data-testid=section-next]").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task Click_LoopsTheSection_RemembersIt_AndMovesThePlayheadIntoIt()
    {
        var page = RenderReady();
        Position(5);

        await page.InvokeAsync(() => page.Find(Id(Solo)).Click());

        await Engine.Received(1).SetLoopAsync(new LoopRegionDto(40.1, 56.1, true, Solo));
        await Engine.Received(1).SeekAsync(40.1);
        page.Find(Id(Solo)).GetAttribute("aria-pressed").ShouldBe("true");
        page.Find(Id(Solo)).GetAttribute("data-looped").ShouldBe("true");
        page.Find("[data-testid=loop-toggle]").GetAttribute("aria-pressed").ShouldBe("true");
    }

    [Fact]
    public async Task Click_InsideTheSection_DoesNotSeek()
    {
        var page = RenderReady();
        Position(42);

        await page.InvokeAsync(() => page.Find(Id(Solo)).Click());

        await Engine.DidNotReceive().SeekAsync(Arg.Any<double>());
    }

    [Fact]
    public void CurrentSection_FollowsThePosition()
    {
        var page = RenderReady();

        Position(10);

        page.WaitForAssertion(() => page.Find(Id(Verse)).GetAttribute("data-current").ShouldBe("true"));
        page.Find(Id(Verse)).GetAttribute("aria-current").ShouldBe("true");
        page.Find(Id(Solo)).GetAttribute("data-current").ShouldBe("false");
        page.Find(Id(Verse)).TextContent.ShouldContain("playing now");
    }

    [Fact]
    public async Task NavigationButtons_UseTheSectionCommands()
    {
        var page = RenderReady();
        Position(12);

        await page.InvokeAsync(() => page.Find("[data-testid=section-next]").Click());
        await Engine.Received(1).SeekAsync(24.1);
        await page.InvokeAsync(() => page.Find("[data-testid=section-restart]").Click());
        await Engine.Received(1).SeekAsync(8.1);
        Position(9);
        await page.InvokeAsync(() => page.Find("[data-testid=section-previous]").Click());
        await Engine.Received(1).SeekAsync(0);
    }

    [Fact]
    public async Task LoopCurrent_AndNextWhileLooping_MoveTheLoopPartByPart()
    {
        var page = RenderReady();
        Position(12);

        await page.InvokeAsync(() => page.Find("[data-testid=section-loop-current]").Click());
        await Engine.Received(1).SetLoopAsync(new LoopRegionDto(8.1, 24.1, true, Verse));
        await page.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Mixer, "mixer.section.next"));

        await Engine.Received(1).SetLoopAsync(new LoopRegionDto(24.1, 40.1, true, PracticeFlowData.SectionIds[2]));
        await Engine.Received(1).SeekAsync(24.1);
    }

    [Fact]
    public async Task SectionCommands_WithoutSections_DoNothing()
    {
        Api.GetSectionsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.SectionSet(sections: []));
        var page = RenderMixer();
        page.WaitForAssertion(() => Interop.HasShortcuts(ShortcutScope.Mixer).ShouldBeTrue());
        Engine.ClearReceivedCalls();

        foreach (var command in new[] { "mixer.loop.section", "mixer.section.next", "mixer.section.previous", "mixer.section.restart" })
        {
            await page.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Mixer, command));
        }

        Engine.ReceivedCalls().ShouldBeEmpty();
        page.Find("[data-testid=section-loop-current]").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task MovingAByHand_ForgetsTheSection_AndTheTrainerRegionSaysSo()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        page.WaitForAssertion(() => page.FindAll("[data-testid^=section-0b1c]").Count.ShouldBe(5));
        Position(42);
        await page.InvokeAsync(() => page.Find(Id(Solo)).Click());
        page.WaitForAssertion(() => page.Find("[data-testid=trainer-region-label]").TextContent.ShouldBe("Region: Guitar solo (0:40-0:56)"));

        await page.InvokeAsync(() => page.Find("[data-testid=loop-set-a]").Click());

        page.Find("[data-testid=trainer-region-label]").TextContent.ShouldBe("Region: A-B loop (0:42-0:56)");
        page.Find(Id(Solo)).GetAttribute("aria-pressed").ShouldBe("false");
    }

    [Fact]
    public async Task LoopOffAndOnAgain_KeepsTheSection()
    {
        var page = RenderReady();
        await page.InvokeAsync(() => page.Find(Id(Solo)).Click());

        await page.InvokeAsync(() => page.Find("[data-testid=loop-toggle]").Click());
        page.Find(Id(Solo)).GetAttribute("aria-pressed").ShouldBe("false");
        await page.InvokeAsync(() => page.Find("[data-testid=loop-toggle]").Click());

        page.Find(Id(Solo)).GetAttribute("aria-pressed").ShouldBe("true");
    }

    [Fact]
    public void Lane_IsAListOfNamedButtons_WithVisibleTextInTheName()
    {
        var page = RenderReady();

        foreach (var button in page.FindAll("[data-testid=section-lane] button"))
        {
            button.ParentElement!.TagName.ShouldBe("LI");
            button.HasAttribute("aria-label").ShouldBeFalse();
            button.GetAttribute("aria-pressed").ShouldBeOneOf("true", "false");
        }

        AssertEveryControlIsNamed(page);
    }

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level) =>
        Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<MixerPage>(m => m.Add(x => x.Id, TestData.TrackId)));
}

/// <summary>T058: <see cref="SectionLane"/> parts that need no mixer.</summary>
public sealed class SectionLaneUnitTests : PageTestContext
{
    [Fact]
    public void WithoutASet_TheLaneSaysItIsDetecting_AndHasStatusNone()
    {
        Api.GetSectionsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<SectionSetDto>(new ApiException("x")));

        var lane = Render<SectionLane>(p => p.Add(c => c.TrackId, TestData.TrackId));

        lane.Find("[data-testid=section-lane]").GetAttribute("data-status").ShouldBe("none");
        lane.Find("[data-testid=section-status]").TextContent.ShouldBe("Detecting sections...");
    }

    [Fact]
    public void ItemExtra_AndActions_AreRendered()
    {
        Api.GetSectionsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.SectionSet());
        var lane = Render<SectionLane>(p => p
            .Add(c => c.TrackId, TestData.TrackId)
            .Add(c => c.Set, PracticeFlowData.SectionSet())
            .Add(c => c.ItemExtra, (SectionDto s) => (RenderFragment)(b => b.AddContent(0, $"x-{s.Label}")))
            .Add(c => c.Actions, (RenderFragment)(b => b.AddContent(0, "edit-actions"))));

        lane.Markup.ShouldContain("x-Outro");
        lane.Markup.ShouldContain("edit-actions");
    }
}

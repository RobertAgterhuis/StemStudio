using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Components.Recording;
using StemStudio.Web.Experience;
using StemStudio.Web.Recording;
using static StemStudio.Web.Tests.Recording.RecordingData;

namespace StemStudio.Web.Tests.Recording;

/// <summary>015 T056: loop sessions, passes, partial badge, star and "Delete the other passes" (FR-010, FR-014).</summary>
public sealed class TakesPanelSessionTests : RecordingTestContext
{
    private static readonly Guid Pass1 = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly Guid Pass2 = Guid.Parse("10000000-0000-4000-8000-000000000002");
    private static readonly Guid Pass3 = Guid.Parse("10000000-0000-4000-8000-000000000003");
    private static readonly Guid Single = Guid.Parse("20000000-0000-4000-8000-000000000001");
    private static readonly Guid OtherSession = Guid.Parse("30000000-0000-4000-8000-000000000001");

    public TakesPanelSessionTests()
    {
        Api.ListTakesAsync(TrackId, Arg.Any<CancellationToken>()).Returns(List(
            Pass(Pass1, 1, false, favorite: false),
            Pass(Pass2, 2, false, favorite: true),
            Pass(Pass3, 3, true, favorite: false),
            Take(Single, number: 4) with { SessionId = OtherSession, RecordedAt = Take().RecordedAt.AddMinutes(-5) }));
        Api.DeleteTakesAsync(TrackId, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>()).Returns(2);
        Confirm.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
    }

    [Fact]
    public async Task Advanced_GroupsTheLoopSession_WithPassesAndThePartialBadge()
    {
        var panel = await RenderTakesAsync(ExperienceLevel.Advanced);

        var group = panel.Find($"[data-testid=takes-session-{SessionId:D}]");
        group.GetAttribute("data-passes").ShouldBe("3");
        group.QuerySelectorAll("[data-testid^=rec-take-row-]").Select(r => r.GetAttribute("data-testid"))
            .ShouldBe([$"rec-take-row-{Pass3:D}", $"rec-take-row-{Pass2:D}", $"rec-take-row-{Pass1:D}"]);
        panel.Find($"[data-testid=rec-take-pass-{Pass3:D}]").TextContent.ShouldBe("Pass 3, partial");
        panel.Find($"[data-testid=rec-take-pass-{Pass1:D}]").TextContent.ShouldBe("Pass 1");
        panel.FindAll($"[data-testid=takes-session-{OtherSession:D}]").ShouldBeEmpty();
        panel.FindAll($"[data-testid=rec-take-pass-{Single:D}]").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ExperienceLevel.Beginner)]
    [InlineData(ExperienceLevel.Advanced)]
    public async Task TheRadios_AreGroupedByAFieldset_AndEveryRowIsAListItemOfAPlainList(ExperienceLevel level)
    {
        // axe "listitem" (E2E recording.a11y): an li must sit in a ul/ol that keeps its list role, never in a radiogroup.
        var panel = await RenderTakesAsync(level);

        var group = panel.Find("fieldset");
        group.QuerySelector("legend")!.TextContent.Trim().ShouldBe("Take in the take strip");
        panel.FindAll("[role=radiogroup], [role=none], [role=presentation]").ShouldBeEmpty();
        foreach (var item in panel.FindAll("li"))
        {
            item.ParentElement!.TagName.ShouldBe("UL");
            item.ParentElement.HasAttribute("role").ShouldBeFalse();
        }

        group.QuerySelectorAll("input[type=radio]").Select(r => r.GetAttribute("name")).Distinct().ShouldBe(["rec-take"]);
    }

    [Fact]
    public async Task Beginner_ShowsFlatRowsWithoutPassesOrGroups()
    {
        var panel = await RenderTakesAsync(ExperienceLevel.Beginner);

        panel.FindAll("[data-testid^=takes-session-]").ShouldBeEmpty();
        panel.FindAll("[data-testid^=rec-take-pass-]").ShouldBeEmpty();
        panel.FindAll("[data-testid^=takes-keep-starred-]").ShouldBeEmpty();
        panel.FindAll("[data-testid^=rec-take-row-]").Count.ShouldBe(4);
    }

    [Fact]
    public async Task Star_TogglesThroughTheApi_WithAriaPressed()
    {
        Api.UpdateTakeAsync(Pass1, Arg.Any<UpdateTakeRequest>(), Arg.Any<CancellationToken>()).Returns(Pass(Pass1, 1, false, favorite: true));
        var panel = await RenderTakesAsync(ExperienceLevel.Beginner);
        var star = panel.Find($"[data-testid=rec-take-star-{Pass1:D}]");
        (star.GetAttribute("aria-pressed"), star.GetAttribute("aria-label")).ShouldBe(("false", "Star Take 1"));

        star.Click();

        await Api.Received(1).UpdateTakeAsync(Pass1, new UpdateTakeRequest { Favorite = true }, Arg.Any<CancellationToken>());
        panel.Find($"[data-testid=rec-take-star-{Pass1:D}]").GetAttribute("aria-pressed").ShouldBe("true");
    }

    [Fact]
    public async Task KeepStarred_AfterConfirmation_DeletesTheOtherSavedPasses()
    {
        var panel = await RenderTakesAsync(ExperienceLevel.Advanced);

        panel.Find($"[data-testid=takes-keep-starred-{SessionId:D}]").Click();

        await Confirm.Received(1).ConfirmAsync("Delete the other passes", Arg.Is<string>(m => m.Contains("2 passes", StringComparison.Ordinal)), "Delete");
        await Api.Received(1).DeleteTakesAsync(TrackId, Arg.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(new[] { Pass3, Pass1 })), Arg.Any<CancellationToken>());
        panel.WaitForAssertion(() => panel.FindAll("[data-testid^=rec-take-row-]").Count.ShouldBe(2));
        panel.FindAll($"[data-testid=takes-keep-starred-{SessionId:D}]").ShouldBeEmpty();
    }

    [Fact]
    public async Task KeepStarred_Cancelled_DeletesNothing()
    {
        Confirm.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var panel = await RenderTakesAsync(ExperienceLevel.Advanced);

        panel.Find($"[data-testid=takes-keep-starred-{SessionId:D}]").Click();

        await Api.DidNotReceive().DeleteTakesAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task KeepStarred_UnloadsADeletedTakeFromTheStrip()
    {
        var panel = await RenderTakesAsync(ExperienceLevel.Advanced);
        panel.Find($"[data-testid=rec-take-select-{Pass1:D}]").Change(true);

        panel.Find($"[data-testid=takes-keep-starred-{SessionId:D}]").Click();

        panel.WaitForAssertion(() => Session.Takes.Loaded.ShouldBeNull());
        await RecordingEngine.Received(1).UnloadTakeAsync();
    }

    [Fact]
    public async Task KeepStarred_IsAbsentWithoutAStar()
    {
        Api.ListTakesAsync(TrackId, Arg.Any<CancellationToken>()).Returns(List(Pass(Pass1, 1, false, false), Pass(Pass2, 2, false, false)));

        var panel = await RenderTakesAsync(ExperienceLevel.Advanced);

        panel.FindAll("[data-testid^=takes-keep-starred-]").ShouldBeEmpty();
    }

    [Fact]
    public async Task FailingDelete_KeepsTheRows_AndShowsTheError()
    {
        Api.DeleteTakesAsync(TrackId, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(System.Net.HttpStatusCode.ServiceUnavailable, "The server is busy"));
        var panel = await RenderTakesAsync(ExperienceLevel.Advanced);

        panel.Find($"[data-testid=takes-keep-starred-{SessionId:D}]").Click();

        panel.WaitForAssertion(() => panel.Find("[data-testid=takes-error]").TextContent.ShouldContain("busy"));
        panel.FindAll("[data-testid^=rec-take-row-]").Count.ShouldBe(4);
    }

    [Fact]
    public async Task FailingStar_ShowsTheError()
    {
        Api.UpdateTakeAsync(Pass1, Arg.Any<UpdateTakeRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(System.Net.HttpStatusCode.NotFound, "The take no longer exists"));
        var panel = await RenderTakesAsync(ExperienceLevel.Beginner);

        panel.Find($"[data-testid=rec-take-star-{Pass1:D}]").Click();

        panel.WaitForAssertion(() => panel.Find("[data-testid=takes-error]").TextContent.ShouldContain("no longer exists"));
    }

    [Fact]
    public void PartialWithoutPass_IsBadgedPartial()
    {
        var item = TakeItem.Saved(Take() with { Partial = true });

        var row = RenderAtLevel<TakeRow>(ExperienceLevel.Advanced, p => p.Add(c => c.Item, item));

        row.Find($"[data-testid=rec-take-pass-{TakeId:D}]").TextContent.ShouldBe("partial");
    }

    private static RecordingTakeDto Pass(Guid id, int pass, bool partial, bool favorite) =>
        Take(id, number: pass) with { PassIndex = pass, Partial = partial, Favorite = favorite };

    private async Task<IRenderedComponent<CascadingValue<ExperienceContext>>> RenderTakesAsync(ExperienceLevel level)
    {
        await OpenWithInputAsync();
        return RenderAtLevel<TakesPanel>(level, p => p.Add(c => c.Takes, Session.Takes).Add(c => c.TrackId, TrackId));
    }
}

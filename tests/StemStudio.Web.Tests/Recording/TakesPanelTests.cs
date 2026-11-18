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

/// <summary>015 T068: Takes panel and take rows - details per level, play from start, rename, delete, retry, warnings (FR-014).</summary>
public sealed class TakesPanelTests : RecordingTestContext
{
    private static readonly Guid Failed = Guid.Parse("40000000-0000-4000-8000-000000000001");
    private readonly List<double> _seeks = [];

    public TakesPanelTests()
    {
        Api.ListTakesAsync(TrackId, Arg.Any<CancellationToken>()).Returns(List(
            Take() with { ClippedSamples = 12, Dropouts = 3 },
            Take(Failed, TakeStatusDto.Failed, 2) with { Error = "The worker stopped", RecordedAt = Take().RecordedAt.AddMinutes(-1) }));
        Confirm.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
    }

    private static string Row => $"{TakeId:D}";

    [Theory]
    [InlineData(ExperienceLevel.Beginner, false, false)]
    [InlineData(ExperienceLevel.Advanced, true, false)]
    [InlineData(ExperienceLevel.Expert, true, true)]
    public async Task Details_FollowTheLevel(ExperienceLevel level, bool latency, bool details)
    {
        var panel = await RenderTakesAsync(level);

        panel.FindAll($"[data-testid=rec-take-latency-{Row}]").Count.ShouldBe(latency ? 1 : 0);
        panel.FindAll($"[data-testid=rec-take-details-{Row}]").Count.ShouldBe(details ? 1 : 0);
        foreach (var id in new[] { "warning", "star", "play-from", "rename", "export", "delete" })
        {
            panel.FindAll($"[data-testid=rec-take-{id}-{Row}]").Count.ShouldBe(1, id);
        }
    }

    [Fact]
    public async Task LatencyBadge_AndExpertDetails_ShowTheFacts()
    {
        var panel = await RenderTakesAsync(ExperienceLevel.Expert);

        var badge = panel.Find($"[data-testid=rec-take-latency-{Row}]");
        (badge.GetAttribute("data-method"), badge.TextContent).ShouldBe(("loopback", "calibrated 25.0 ms"));
        panel.Find($"[data-testid=rec-take-details-{Row}]").TextContent.ShouldBe("USB Mic, mono-1, 1102 samples latency, 3 dropouts, 12 clipped samples");
    }

    [Fact]
    public async Task Warnings_NameEveryProblem_WithTheFirstAsCode()
    {
        var panel = await RenderTakesAsync(ExperienceLevel.Beginner);

        var warning = panel.Find($"[data-testid=rec-take-warning-{Row}]");
        warning.GetAttribute("data-warning").ShouldBe("clipped");
        warning.TextContent.ShouldContain("3 dropouts");
        TakeRowText.Warning(Take() with { Quiet = true })!.Code.ShouldBe("quiet");
        TakeRowText.Warning(Take()).ShouldBeNull();
        TakeRowText.Latency(Take() with { Latency = Take().Latency with { Method = LatencyMethodDto.Estimated } }).ShouldStartWith("estimated");
        TakeRowText.Latency(Take() with { Latency = Take().Latency with { Method = LatencyMethodDto.Manual } }).ShouldStartWith("manual");
    }

    [Fact]
    public async Task PlayFrom_LoadsTheTake_AndSeeksToItsStart()
    {
        var panel = await RenderTakesAsync(ExperienceLevel.Beginner);

        panel.Find($"[data-testid=rec-take-play-from-{Row}]").Click();

        _seeks.ShouldBe([10.0]);
        Session.Takes.Loaded!.Id.ShouldBe(Row);
        panel.Find($"[data-testid=rec-take-play-from-{Row}]").Click();
        await RecordingEngine.Received(1).LoadTakeAsync(Arg.Any<LoadTakeRequest>());
    }

    [Fact]
    public async Task Rename_TrimsAndStoresTheName()
    {
        Api.UpdateTakeAsync(TakeId, Arg.Any<UpdateTakeRequest>(), Arg.Any<CancellationToken>()).Returns(Take() with { Name = "Solo try" });
        var panel = await RenderTakesAsync(ExperienceLevel.Beginner);

        panel.Find($"[data-testid=rec-take-rename-{Row}]").Click();
        panel.Find("[data-testid=rec-take-rename-input]").GetAttribute("value").ShouldBe("Take 1");
        panel.Find("[data-testid=rec-take-rename-input]").Input("  Solo try  ");
        panel.Find("[data-testid=rec-take-rename-confirm]").Click();

        await Api.Received(1).UpdateTakeAsync(TakeId, new UpdateTakeRequest { Name = "Solo try" }, Arg.Any<CancellationToken>());
        panel.Find($"[data-testid=rec-take-name-{Row}]").TextContent.ShouldBe("Solo try");
        panel.FindAll("[data-testid=rec-take-rename-input]").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("x")]
    public async Task Rename_RefusesEmptyOrTooLongNames(string start)
    {
        var name = start == "x" ? new string('x', 81) : start;
        var panel = await RenderTakesAsync(ExperienceLevel.Beginner);
        panel.Find($"[data-testid=rec-take-rename-{Row}]").Click();

        panel.Find("[data-testid=rec-take-rename-input]").Input(name);
        panel.Find("[data-testid=rec-take-rename-input]").KeyDown("Enter");

        panel.Find("[role=alert]").TextContent.ShouldContain("1 to 80 characters");
        await Api.DidNotReceive().UpdateTakeAsync(Arg.Any<Guid>(), Arg.Any<UpdateTakeRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rename_EscapeCancels()
    {
        var panel = await RenderTakesAsync(ExperienceLevel.Beginner);
        panel.Find($"[data-testid=rec-take-rename-{Row}]").Click();

        panel.Find("[data-testid=rec-take-rename-input]").KeyDown("Escape");

        panel.FindAll("[data-testid=rec-take-rename-input]").ShouldBeEmpty();
        panel.Find($"[data-testid=rec-take-rename-{Row}]").Click();
        panel.Find("[data-testid=rec-take-rename-input]").KeyDown("a");
        panel.FindAll("[data-testid=rec-take-rename-input]").Count.ShouldBe(1);
    }

    [Fact]
    public async Task Rename_ServerRefusal_IsShown()
    {
        Api.UpdateTakeAsync(TakeId, Arg.Any<UpdateTakeRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(System.Net.HttpStatusCode.NotFound, "The take no longer exists"));
        var panel = await RenderTakesAsync(ExperienceLevel.Beginner);
        panel.Find($"[data-testid=rec-take-rename-{Row}]").Click();

        panel.Find("[data-testid=rec-take-rename-confirm]").Click();

        panel.Find("[role=alert]").TextContent.ShouldContain("no longer exists");
    }

    [Fact]
    public async Task Delete_Confirmed_RemovesTheRow_AndUnloadsTheStrip()
    {
        var panel = await RenderTakesAsync(ExperienceLevel.Beginner);
        panel.Find($"[data-testid=rec-take-select-{Row}]").Change(true);

        panel.Find($"[data-testid=rec-take-delete-{Row}]").Click();

        await Confirm.Received(1).ConfirmAsync("Delete take", Arg.Is<string>(m => m.Contains("Take 1", StringComparison.Ordinal)), "Delete");
        await Api.Received(1).DeleteTakeAsync(TakeId, Arg.Any<CancellationToken>());
        panel.FindAll($"[data-testid=rec-take-row-{Row}]").ShouldBeEmpty();
        Session.Takes.Loaded.ShouldBeNull();
    }

    [Fact]
    public async Task Delete_Cancelled_KeepsTheTake()
    {
        Confirm.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var panel = await RenderTakesAsync(ExperienceLevel.Beginner);

        panel.Find($"[data-testid=rec-take-delete-{Row}]").Click();

        await Api.DidNotReceive().DeleteTakeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FailedTake_ShowsItsErrorAndRetry()
    {
        Api.RetryTakeAsync(Failed, Arg.Any<CancellationToken>()).Returns(Take(Failed, TakeStatusDto.Saving, 2));
        var panel = await RenderTakesAsync(ExperienceLevel.Beginner);

        panel.Find($"[data-testid=rec-take-error-{Failed:D}]").TextContent.ShouldBe("The worker stopped");
        panel.FindAll($"[data-testid=rec-take-retry-{Row}]").ShouldBeEmpty();
        panel.Find($"[data-testid=rec-take-retry-{Failed:D}]").Click();

        panel.Find($"[data-testid=rec-take-row-{Failed:D}]").GetAttribute("data-status").ShouldBe("saving");
        panel.FindAll($"[data-testid=rec-take-retry-{Failed:D}]").ShouldBeEmpty();
    }

    [Fact]
    public async Task FailedRetry_IsShown()
    {
        Api.RetryTakeAsync(Failed, Arg.Any<CancellationToken>()).ThrowsAsync(Problem(System.Net.HttpStatusCode.Conflict, "The capture is missing"));
        var panel = await RenderTakesAsync(ExperienceLevel.Beginner);

        panel.Find($"[data-testid=rec-take-retry-{Failed:D}]").Click();

        panel.Find("[data-testid=takes-error]").TextContent.ShouldContain("capture is missing");
    }

    [Fact]
    public async Task Export_RaisesTheRequestWithTheTake()
    {
        TakeItem? asked = null;
        await OpenWithInputAsync();
        var panel = RenderAtLevel<TakesPanel>(ExperienceLevel.Beginner, p => p
            .Add(c => c.Takes, Session.Takes)
            .Add(c => c.OnExport, (TakeItem item) => asked = item));

        panel.Find($"[data-testid=rec-take-export-{Row}]").Click();

        asked!.Id.ShouldBe(Row);
        panel.Find($"[data-testid=rec-take-export-{Failed:D}]").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task EveryRowButton_HasANameContainingTheTakeName()
    {
        var panel = await RenderTakesAsync(ExperienceLevel.Expert);

        foreach (var button in panel.Find($"[data-testid=rec-take-row-{Row}]").QuerySelectorAll("button[aria-label]"))
        {
            button.GetAttribute("aria-label")!.ShouldContain("Take 1");
        }
    }

    private async Task<IRenderedComponent<CascadingValue<ExperienceContext>>> RenderTakesAsync(ExperienceLevel level)
    {
        await OpenWithInputAsync();
        return RenderAtLevel<TakesPanel>(level, p => p
            .Add(c => c.Takes, Session.Takes)
            .Add(c => c.TrackId, TrackId)
            .Add(c => c.OnSeek, (double s) => _seeks.Add(s)));
    }
}

using Bunit;
using NSubstitute;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Components.Recording;
using StemStudio.Web.Recording;
using static StemStudio.Web.Tests.Recording.RecordingData;

namespace StemStudio.Web.Tests.Recording;

/// <summary>015 T042/T043 (US1 part of the Takes panel): rows with status, selection into the strip, empty state.</summary>
public sealed class TakesPanelBasicTests : RecordingTestContext
{
    [Fact]
    public void WithoutTakes_ShowsTheEmptyState() =>
        Render<TakesPanel>(p => p.Add(c => c.Takes, Session.Takes)).Find("[data-testid=takes-empty]").TextContent.ShouldContain("No takes");

    [Fact]
    public async Task Rows_ShowStatusNamePartAndRange_NewestFirst()
    {
        var saving = Take(Guid.NewGuid(), TakeStatusDto.Saving, 2) with { RecordedAt = new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.Zero) };
        Api.ListTakesAsync(TrackId, Arg.Any<CancellationToken>()).Returns(List(Take(), saving, Take(Guid.NewGuid(), TakeStatusDto.Failed, 3)));
        await OpenWithInputAsync();

        var panel = Render<TakesPanel>(p => p.Add(c => c.Takes, Session.Takes));

        panel.FindAll("[data-testid^=rec-take-row-]").Select(r => r.GetAttribute("data-status")).ShouldBe(["saving", "ready", "failed"]);
        var row = $"{TakeId:D}";
        panel.Find($"[data-testid=rec-take-name-{row}]").TextContent.ShouldBe("Take 1");
        panel.Find($"[data-testid=rec-take-part-{row}]").GetAttribute("data-part").ShouldBe("guitar");
        panel.Find($"[data-testid=rec-take-range-{row}]").TextContent.ShouldBe("0:10.0 - 0:18.0");
        panel.Find("[data-testid=takes-total-size]").TextContent.ShouldContain("3 takes");
    }

    [Fact]
    public async Task SelectingARow_LoadsTheSavedTakeIntoTheStrip()
    {
        var changed = 0;
        Api.ListTakesAsync(TrackId, Arg.Any<CancellationToken>()).Returns(List(Take()));
        await OpenWithInputAsync();
        var panel = Render<TakesPanel>(p => p.Add(c => c.Takes, Session.Takes).Add(c => c.OnChanged, () => changed++));

        panel.Find($"[data-testid=rec-take-select-{TakeId:D}]").Change(true);

        await RecordingEngine.Received(1).LoadTakeAsync(Arg.Is<LoadTakeRequest>(r => r.Source.Url == $"http://api.test/api/takes/{TakeId:D}/audio"));
        panel.Find($"[data-testid=rec-take-row-{TakeId:D}]").GetAttribute("data-selected").ShouldBe("true");
        changed.ShouldBe(1);
    }

    [Fact]
    public async Task ALocalCapture_IsListedAsLocalUntilItsUploadIsAccepted()
    {
        await OpenWithInputAsync();
        var item = TakeItem.Local(Capture("c1"), Upload());
        await Session.Takes.AddAsync([item]);
        item.Fail("refused");

        var panel = Render<TakesPanel>(p => p.Add(c => c.Takes, Session.Takes));

        panel.Find("[data-testid=rec-take-row-c1]").GetAttribute("data-status").ShouldBe("local");
        panel.Find("[data-testid=rec-take-row-c1]").TextContent.ShouldContain("Not saved");
        item.Name.ShouldBe("New take");
    }
}

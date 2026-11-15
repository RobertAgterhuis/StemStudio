using Bunit;
using NSubstitute;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Mixer;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer;

public sealed class MetronomePanelTests : MixerTestContext
{
    private static readonly MetronomeSettingsDto Detected = new(true, 0.7, null, 0, 4);

    [Fact]
    public void Defaults_FollowDetectedGrid()
    {
        var page = RenderMixer();

        page.Find("[data-testid=metronome-toggle]").GetAttribute("aria-pressed").ShouldBe("true");
        page.Find("[data-testid=metronome-volume]").GetAttribute("aria-valuenow").ShouldBe("70");
        page.Find("[data-testid=metronome-bpm]").GetAttribute("value").ShouldBeNull();
        page.Find("[data-testid=metronome-bpm]").GetAttribute("placeholder").ShouldBe("113.5");
        page.Find("[data-testid=metronome-offset]").GetAttribute("value").ShouldBe("0");
        page.Find("[data-testid=metronome-beats-per-bar]").GetAttribute("value").ShouldBe("4");
        page.FindAll("[data-testid=metronome-error]").ShouldBeEmpty();
    }

    [Fact]
    public void UnreliableGrid_StartsDisabled()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>())
            .Returns(TestData.Track() with { BeatGrid = TestData.Grid(reliable: false) });

        var page = RenderMixer();

        page.Find("[data-testid=metronome-toggle]").GetAttribute("aria-pressed").ShouldBe("false");
        page.Find("[data-testid=metronome-hint]").TextContent.ShouldContain("not reliable");
    }

    [Fact]
    public async Task EachChange_CallsSetMetronome_AndMarksDirty()
    {
        var page = RenderMixer();
        Engine.ClearReceivedCalls();

        await page.Find("[data-testid=metronome-toggle]").ClickAsync(new());
        SetValue(page.Find("[data-testid=metronome-volume]"), 40);
        page.Find("[data-testid=metronome-bpm]").Change("120");
        page.Find("[data-testid=metronome-offset]").Change("-150");
        page.Find("[data-testid=metronome-beats-per-bar]").Change("3");

        Received.InOrder(() =>
        {
            Engine.SetMetronomeAsync(Detected with { Enabled = false });
            Engine.SetMetronomeAsync(Detected with { Enabled = false, Volume = 0.4 });
            Engine.SetMetronomeAsync(Detected with { Enabled = false, Volume = 0.4, BpmOverride = 120 });
            Engine.SetMetronomeAsync(Detected with { Enabled = false, Volume = 0.4, BpmOverride = 120, OffsetMs = -150 });
            Engine.SetMetronomeAsync(new MetronomeSettingsDto(false, 0.4, 120, -150, 3));
        });
        page.Find("[data-testid=unsaved-indicator]").ShouldNotBeNull();
        page.Find("[data-testid=metronome-bpm]").GetAttribute("value").ShouldBe("120");
        page.Find("[data-testid=metronome-toggle]").GetAttribute("aria-pressed").ShouldBe("false");
    }

    [Theory]
    [InlineData("metronome-bpm", "29", MetronomeRules.BpmMessage)]
    [InlineData("metronome-bpm", "301", MetronomeRules.BpmMessage)]
    [InlineData("metronome-bpm", "fast", MetronomeRules.BpmMessage)]
    [InlineData("metronome-offset", "2001", MetronomeRules.OffsetMessage)]
    [InlineData("metronome-offset", "1.5", MetronomeRules.OffsetMessage)]
    [InlineData("metronome-beats-per-bar", "0", MetronomeRules.BeatsPerBarMessage)]
    [InlineData("metronome-beats-per-bar", "13", MetronomeRules.BeatsPerBarMessage)]
    [InlineData("metronome-beats-per-bar", "", MetronomeRules.BeatsPerBarMessage)]
    public async Task OutOfRange_IsRefusedWithMessage(string testId, string value, string message)
    {
        var page = RenderMixer();
        Engine.ClearReceivedCalls();

        page.Find($"[data-testid={testId}]").Change(value);

        page.Find("[data-testid=metronome-error]").TextContent.ShouldBe(message);
        await Engine.DidNotReceiveWithAnyArgs().SetMetronomeAsync(default!);
        page.FindAll("[data-testid=unsaved-indicator]").ShouldBeEmpty();
    }

    [Fact]
    public void ValidInput_ClearsPreviousError()
    {
        var page = RenderMixer();

        page.Find("[data-testid=metronome-bpm]").Change("10");
        page.Find("[data-testid=metronome-bpm]").Change("90");

        page.FindAll("[data-testid=metronome-error]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Reset_RestoresDetectedGrid_KeepsToggleAndVolume()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>())
            .Returns(TestData.Track() with { BeatGrid = TestData.Grid(beatsPerBar: 3) });
        var page = RenderMixer();
        page.Find("[data-testid=metronome-bpm]").Change("140");
        page.Find("[data-testid=metronome-offset]").Change("200");
        page.Find("[data-testid=metronome-beats-per-bar]").Change("7");
        SetValue(page.Find("[data-testid=metronome-volume]"), 50);
        Engine.ClearReceivedCalls();

        await page.Find("[data-testid=metronome-reset]").ClickAsync(new());

        await Engine.Received(1).SetMetronomeAsync(new MetronomeSettingsDto(true, 0.5, null, 0, 3));
        page.Find("[data-testid=metronome-beats-per-bar]").GetAttribute("value").ShouldBe("3");
        page.Find("[data-testid=metronome-offset]").GetAttribute("value").ShouldBe("0");
    }

    [Fact]
    public void ClearingBpm_FollowsDetectedGridAgain()
    {
        var page = RenderMixer();
        page.Find("[data-testid=metronome-bpm]").Change("100");
        Engine.ClearReceivedCalls();

        page.Find("[data-testid=metronome-bpm]").Change(" ");

        Engine.Received(1).SetMetronomeAsync(Detected);
    }

    [Fact]
    public void Rules_AcceptBoundaries()
    {
        MetronomeRules.Bpm(Detected, "30").Settings!.BpmOverride.ShouldBe(30);
        MetronomeRules.Bpm(Detected, "300").Settings!.BpmOverride.ShouldBe(300);
        MetronomeRules.Bpm(Detected, null).Settings!.BpmOverride.ShouldBeNull();
        MetronomeRules.Offset(Detected, "-2000").Settings!.OffsetMs.ShouldBe(-2000);
        MetronomeRules.Offset(Detected, "+2000").Settings!.OffsetMs.ShouldBe(2000);
        MetronomeRules.BeatsPerBar(Detected, "1").Settings!.BeatsPerBar.ShouldBe(1);
        MetronomeRules.BeatsPerBar(Detected, "12").Settings!.BeatsPerBar.ShouldBe(12);
        MetronomeRules.Volume(Detected, 1.5).Volume.ShouldBe(1);
    }

    [Fact]
    public void NoGrid_HasNoBpmPlaceholder()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track() with { BeatGrid = null });

        var page = RenderMixer();

        page.Find("[data-testid=metronome-bpm]").GetAttribute("placeholder").ShouldBe("BPM");
        page.Find("[data-testid=metronome-hint]").TextContent.ShouldContain("Enter a BPM");
    }
}

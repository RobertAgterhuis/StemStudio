using Bunit;
using NSubstitute;
using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Mixer;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Mixer;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer;

public sealed class MixerComponentTests : MixerTestContext
{
    [Fact]
    public void EngineSequence_LoadThenMetersThenFullMix()
    {
        RenderMixer();

        var calls = Engine.ReceivedCalls().Select(c => c.GetMethodInfo().Name).Where(n => !n.StartsWith("add_", StringComparison.Ordinal)).ToList();
        calls.ShouldBe(
        [
            "InitializeAsync", "LoadTrackAsync",
            .. Enumerable.Repeat("AttachMeterAsync", 7),
            .. Enumerable.Repeat("SetChannelAsync", 6),
            "SetMasterAsync", "SetMetronomeAsync",
        ]);
    }

    [Fact]
    public void NoMetronomeMeter_IsRendered()
    {
        var page = RenderMixer();

        page.FindAll("[data-testid^=meter-]").Count.ShouldBe(7);
        page.FindAll("[data-testid=meter-metronome]").ShouldBeEmpty();
    }

    [Fact]
    public void Seek_DoesNotMovePosition_UntilEngineReportsIt()
    {
        var page = RenderMixer();

        page.Find("[data-testid=transport-seek]").Change("30");

        page.Find("[data-testid=transport-position]").TextContent.ShouldBe("0:00.0 / 3:33");
        Engine.PositionChanged += Raise.Event<EventHandler<double>>(Engine, 10.0);
        page.WaitForAssertion(() => page.Find("[data-testid=transport-position]").TextContent.ShouldBe("0:10.0 / 3:33"));
    }

    [Fact]
    public void Mixer_ShowsLoading_WhileTrackIsFetched()
    {
        var pending = new TaskCompletionSource<TrackDto>();
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(pending.Task);

        var page = RenderMixer();

        page.Markup.ShouldContain("Loading...");
        Engine.ReceivedCalls().ShouldBeEmpty();
        pending.SetResult(TestData.Track());
        page.WaitForAssertion(() => page.FindAll("[data-testid=strip-vocals]").Count.ShouldBe(1));
    }

    [Fact]
    public void Mixer_ShowsLoadingStems_UntilEngineIsReady()
    {
        var pending = new TaskCompletionSource<double>();
        Engine.LoadTrackAsync(Arg.Any<TrackDto>()).Returns(pending.Task);

        var page = RenderMixer();

        page.Find("[data-testid=mixer-loading]").TextContent.ShouldBe("Loading stems...");
        page.Find("[data-testid=transport-play]").HasAttribute("disabled").ShouldBeTrue();
        pending.SetResult(200);
        page.WaitForAssertion(() => page.FindAll("[data-testid=mixer-loading]").ShouldBeEmpty());
        page.Find("[data-testid=transport-seek]").GetAttribute("max").ShouldBe("200");
        page.Find("[data-testid=transport-play]").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void TrackPages_ShowLoading_WhilePending()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<TrackDto>().Task);
        Api.ListTracksAsync(Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<IReadOnlyList<TrackDto>>().Task);

        Render<TrackDetail>(p => p.Add(c => c.Id, TestData.TrackId)).Markup.ShouldContain("Loading...");
        Render<Tracks>().Markup.ShouldContain("Loading...");
    }

    [Theory]
    [InlineData(-0.25, "25 left")]
    [InlineData(0.5, "50 right")]
    [InlineData(0.0, "centre")]
    public void ChannelStrip_PanText(double pan, string expected)
    {
        var strip = Render<ChannelStrip>(p => p.Add(c => c.Channel, new ChannelState(StemNames.Bass) { Pan = pan }));

        strip.Find("[data-testid=pan-bass]").GetAttribute("aria-valuetext").ShouldBe(expected);
    }

    [Fact]
    public void ChannelStrip_SplatsAdditionalAttributesOnItsRoot()
    {
        var strip = Render<ChannelStrip>(p => p.Add(c => c.Channel, new ChannelState(StemNames.Bass)).AddUnmatched("data-take-id", "take-1"));

        strip.Find("[data-testid=strip-bass]").GetAttribute("data-take-id").ShouldBe("take-1");
    }

    [Fact]
    public void Inputs_IgnoreNonNumbers()
    {
        var volumes = new List<double>();
        var master = Render<MasterSection>(p => p.Add(c => c.Volume, 1).Add(c => c.OnVolume, (double v) => volumes.Add(v)));
        var seeks = new List<double>();
        var transport = Render<TransportBar>(p => p.Add(c => c.Duration, 10).Add(c => c.OnSeek, (double s) => seeks.Add(s)));
        var pans = new List<double>();
        var strip = Render<ChannelStrip>(p => p.Add(c => c.Channel, new ChannelState(StemNames.Bass)).Add(c => c.OnPan, (double v) => pans.Add(v)));

        master.Find("[data-testid=master-fader]").Input("x");
        transport.Find("[data-testid=transport-seek]").Change("x");
        strip.Find("[data-testid=pan-bass]").Input("");
        master.Find("[data-testid=master-fader]").Input("25");

        volumes.ShouldBe([0.25]);
        seeks.ShouldBeEmpty();
        pans.ShouldBeEmpty();
    }

    [Fact]
    public void InputValue_ParsesInvariantNumbers()
    {
        InputValue.Parse(new() { Value = "12.5" }).ShouldBe(12.5);
        InputValue.Parse(new() { Value = null }).ShouldBeNull();
        InputValue.Percent(-0.001).ShouldBe("0");
        InputValue.Format(9.5).ShouldBe("9.5");
    }
}

using Bunit;
using NSubstitute;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Audio;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer.Karaoke;

/// <summary>The banner's place in the mixer (006 T037, FR-005): at the bottom, docked above the transport, attached after loadTrack.</summary>
public sealed class KaraokeMixerTests : MixerTestContext
{
    public KaraokeMixerTests() =>
        Api.GetLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(LyricsData.Found());

    [Fact]
    public void Banner_IsDockedAtTheBottom_AboveTheTransport()
    {
        var page = RenderMixer();

        var dock = page.Find(".ss-mixer-dock");
        dock.QuerySelector("[data-testid=karaoke-banner]").ShouldNotBeNull();
        var children = dock.Children.Select(c => c.ClassList.Contains("ss-transport") ? "transport" : c.ClassList.Contains("ss-karaoke") ? "karaoke" : "other");
        children.ShouldBe(["karaoke", "transport"]);
        page.Find(".ss-console").LastElementChild.ShouldBe(dock);
    }

    [Fact]
    public void Banner_IsAttachedAfterTheTrackIsLoaded()
    {
        var page = RenderMixer();

        page.WaitForAssertion(() => Engine.Received(1).AttachKaraokeAsync("karaoke-host", Arg.Any<KaraokeLyrics>(), Arg.Any<KaraokeOptions>()));
        Received.InOrder(() =>
        {
            Engine.LoadTrackAsync(Arg.Any<TrackDto>());
            Engine.AttachKaraokeAsync("karaoke-host", Arg.Any<KaraokeLyrics>(), Arg.Any<KaraokeOptions>());
        });
    }

    [Fact]
    public void EngineFailure_NeverAttaches()
    {
        Engine.LoadTrackAsync(Arg.Any<TrackDto>()).Returns<double>(_ => throw new AudioEngineException("decode failed"));

        var page = RenderMixer();

        page.WaitForAssertion(() => page.Find("[data-testid=mixer-engine-error]").ShouldNotBeNull());
        Engine.DidNotReceiveWithAnyArgs().AttachKaraokeAsync(default!, default!, default!);
    }
}

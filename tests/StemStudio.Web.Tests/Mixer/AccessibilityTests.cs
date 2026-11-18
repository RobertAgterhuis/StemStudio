using Bunit;
using NSubstitute;
using StemStudio.Web.Audio;
using StemStudio.Web.Interop;

namespace StemStudio.Web.Tests.Mixer;

public sealed class AccessibilityTests : MixerTestContext
{
    [Fact]
    public void EveryControl_HasAnAccessibleName()
    {
        var page = RenderMixer();

        AssertEveryControlIsNamed(page);

        page.Find("[data-testid=fader-vocals]").GetAttribute("aria-label").ShouldBe("vocals volume");
        page.Find("[data-testid=pan-vocals]").GetAttribute("aria-valuetext").ShouldBe("centre");
        page.Find("[data-testid=transport-play]").GetAttribute("aria-label").ShouldBe("Play");
    }

    [Fact]
    public async Task Space_TogglesPlayAndPause_ThroughTheKeyMap()
    {
        RenderMixer();
        Interop.KeyMaps[ShortcutScope.Mixer].Bindings.ShouldContain(b => b.Command == "mixer.play-pause" && b.Chords[0].Key == " ");

        await Interop.CommandAsync(ShortcutScope.Mixer, "mixer.play-pause");
        Engine.PlaybackStateChanged += Raise.Event<EventHandler<PlaybackState>>(Engine, PlaybackState.Playing);
        await Interop.CommandAsync(ShortcutScope.Mixer, "mixer.play-pause");

        Received.InOrder(() =>
        {
            Engine.PlayAsync();
            Engine.PauseAsync();
        });
    }

    [Fact]
    public async Task Dispose_ReleasesTheMixerKeyMap()
    {
        var page = RenderMixer();
        Interop.HasShortcuts(ShortcutScope.Mixer).ShouldBeTrue();

        await page.Instance.DisposeAsync();

        Interop.HasShortcuts(ShortcutScope.Mixer).ShouldBeFalse();
    }
}

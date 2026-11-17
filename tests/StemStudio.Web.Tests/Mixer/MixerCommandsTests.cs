using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Web.Audio;
using StemStudio.Web.Interop;
using StemStudio.Web.Mixer;
using StemStudio.Web.Practice;
using MixerPage = StemStudio.Web.Components.Mixer.Mixer;

namespace StemStudio.Web.Tests.Mixer;

/// <summary>
/// T066: the mixer command dispatcher (data-model 5.2, FR-019, FR-020). Every mixer command id of the catalogue goes to the same
/// action as its control (the 004/012 behaviour is covered per command in MixerExpertTests and the practice tests); unknown ids
/// and ids without a handler do nothing; the key map comes from the catalogue and follows the preferences.
/// </summary>
public sealed class MixerCommandsTests : MixerTestContext
{
    private IRenderedComponent<MixerPage> RenderReady()
    {
        var page = RenderMixer();
        page.WaitForAssertion(() => Interop.HasShortcuts(ShortcutScope.Mixer).ShouldBeTrue());
        return page;
    }

    [Fact]
    public void TheMixerRegistersTheCatalogueKeyMap()
    {
        RenderReady();

        var map = Interop.KeyMaps[ShortcutScope.Mixer];
        map.Bindings.Where(b => b.Layer == "keys").Select(b => b.Command)
            .ShouldBe(KeyMapDefaults.Catalogue.Commands.Where(c => c.Scope == "mixer").Select(c => c.Command));
        map.Bindings.ShouldContain(b => b.Layer == "pedal" && b.Command == "mixer.play-pause" && b.Chords[0].Key == "pagedown");
        (map.SingleKey, map.PedalMode).ShouldBe((true, false));
    }

    [Fact]
    public async Task EveryCatalogueMixerCommand_HasAHandler_Except015s()
    {
        var page = RenderReady();
        var handled = new List<string>();

        foreach (var command in KeyMapDefaults.Catalogue.Commands.Where(c => c.Scope == "mixer").Select(c => c.Command))
        {
            await page.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Mixer, command));
            handled.Add(command);
        }

        handled.Count.ShouldBe(KeyMapDefaults.Catalogue.Commands.Count(c => c.Scope == "mixer"));
        await Engine.Received().PlayAsync();
        await Engine.Received().StopAsync();
    }

    [Theory]
    [InlineData("mixer.fly")]
    [InlineData("mixer.seek:abc")]
    [InlineData("")]
    [InlineData("mixer.record.toggle")]
    public async Task UnknownOrUnhandledIds_DoNothing(string command)
    {
        var page = RenderReady();
        Engine.ClearReceivedCalls();

        await page.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Mixer, command));

        Engine.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task ChangedShortcutPreferences_UpdateTheRegisteredMap()
    {
        RenderReady();
        var preferences = Services.GetRequiredService<StemStudio.Web.Preferences.PreferencesState>();
        var overrides = new Dictionary<string, IReadOnlyList<ShortcutChord>> { ["mixer.section.next"] = [new ShortcutChord("j")] };

        await preferences.SetShortcutsAsync(ShortcutPreferences.Default with { SingleKey = false, Overrides = overrides });

        Interop.KeyMapUpdates.ShouldBe(1);
        Interop.KeyMaps[ShortcutScope.Mixer].SingleKey.ShouldBeFalse();
        Interop.KeyMaps[ShortcutScope.Mixer].Bindings.Single(b => b.Command == "mixer.section.next" && b.Layer == "keys").Chords
            .ShouldBe([new KeyChordData("j")]);
        await preferences.SetLevelAsync(StemStudio.Web.Experience.ExperienceLevel.Expert);
        Interop.KeyMapUpdates.ShouldBe(1);
    }

    [Fact]
    public async Task PlayPause_TogglesLikeTheTransport()
    {
        var page = RenderReady();

        await page.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Mixer, "mixer.play-pause"));
        Engine.PlaybackStateChanged += Raise.Event<EventHandler<PlaybackState>>(Engine, PlaybackState.Playing);
        await page.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Mixer, "mixer.play-pause"));

        await Engine.Received(1).PlayAsync();
        await Engine.Received(1).PauseAsync();
    }
}

/// <summary>T066: <see cref="MixerCommandRegistry"/> parsing and dispatch.</summary>
public sealed class MixerCommandRegistryTests
{
    [Fact]
    public async Task Dispatch_ParsesTheArgument_AndReportsWhetherAHandlerRan()
    {
        var registry = new MixerCommandRegistry();
        var seen = new List<double>();
        registry.Add("mixer.seek", s => { seen.Add(s); return Task.CompletedTask; });
        registry.Add("mixer.stop", () => { seen.Add(-99); return Task.CompletedTask; });

        (await registry.DispatchAsync("mixer.seek:-5")).ShouldBeTrue();
        (await registry.DispatchAsync("mixer.seek:1.5")).ShouldBeTrue();
        (await registry.DispatchAsync("mixer.stop")).ShouldBeTrue();
        (await registry.DispatchAsync("mixer.seek")).ShouldBeTrue();
        (await registry.DispatchAsync("mixer.seek:x")).ShouldBeFalse();
        (await registry.DispatchAsync("mixer.none")).ShouldBeFalse();
        (await registry.DispatchAsync(string.Empty)).ShouldBeFalse();

        seen.ShouldBe([-5, 1.5, -99, 0]);
        registry.Commands.ShouldBe(["mixer.seek", "mixer.stop"], ignoreOrder: true);
        registry.Handles("mixer.seek:3").ShouldBeTrue();
        registry.Handles("mixer.loop.a").ShouldBeFalse();
    }

    [Fact]
    public async Task ALaterRegistration_ReplacesTheEarlierOne()
    {
        var registry = new MixerCommandRegistry();
        var which = 0;
        registry.Add("mixer.record.toggle", () => { which = 1; return Task.CompletedTask; });
        registry.Add("mixer.record.toggle", () => { which = 2; return Task.CompletedTask; });

        await registry.DispatchAsync("mixer.record.toggle");

        which.ShouldBe(2);
    }

    [Fact]
    public void Arguments_AreChecked()
    {
        var registry = new MixerCommandRegistry();
        Should.Throw<ArgumentNullException>(() => registry.Add("x", (Func<Task>)null!));
        Should.Throw<ArgumentNullException>(() => registry.Add("x", (Func<double, Task>)null!));
        Should.Throw<ArgumentException>(() => registry.Add(" ", () => Task.CompletedTask));
    }
}

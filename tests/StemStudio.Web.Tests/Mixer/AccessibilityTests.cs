using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using StemStudio.Web.Audio;
using StemStudio.Web.Services;

namespace StemStudio.Web.Tests.Mixer;

public sealed class AccessibilityTests : MixerTestContext
{
    private Func<Task>? _onSpace;

    public AccessibilityTests()
    {
        var shortcuts = Substitute.For<IKeyboardShortcuts>();
        shortcuts.RegisterSpaceAsync(Arg.Do<Func<Task>>(f => _onSpace = f)).Returns(Substitute.For<IAsyncDisposable>());
        Services.AddSingleton(shortcuts);
    }

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
    public async Task Space_TogglesPlayAndPause()
    {
        RenderMixer();
        _onSpace.ShouldNotBeNull();

        await _onSpace();
        Engine.PlaybackStateChanged += Raise.Event<EventHandler<PlaybackState>>(Engine, PlaybackState.Playing);
        await _onSpace();

        Received.InOrder(() =>
        {
            Engine.PlayAsync();
            Engine.PauseAsync();
        });
    }

    [Fact]
    public async Task Dispose_ReleasesShortcut()
    {
        var registration = Substitute.For<IAsyncDisposable>();
        Services.GetRequiredService<IKeyboardShortcuts>().RegisterSpaceAsync(Arg.Any<Func<Task>>()).Returns(registration);
        var page = RenderMixer();

        await page.Instance.DisposeAsync();

        await registration.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task KeyboardShortcuts_RegistersSpaceListener_AndInvokesCallback()
    {
        var module = JSInterop.SetupModule("./js/mixer-keys.js");
        var listener = module.SetupModule("registerSpace", _ => true);
        listener.SetupVoid("dispose", _ => true).SetVoidResult();
        var shortcuts = new KeyboardShortcuts(JSInterop.JSRuntime);
        var calls = 0;

        var registration = await shortcuts.RegisterSpaceAsync(() =>
        {
            calls++;
            return Task.CompletedTask;
        });
        var callback = module.VerifyInvoke("registerSpace").Arguments.Single().ShouldBeOfType<DotNetObjectReference<SpaceKeyCallback>>();
        await callback.Value.OnSpace();
        await registration.DisposeAsync();

        calls.ShouldBe(1);
        listener.VerifyInvoke("dispose");
    }

    [Fact]
    public async Task KeyboardShortcuts_Dispose_IgnoresDisconnectedCircuit()
    {
        var module = JSInterop.SetupModule("./js/mixer-keys.js");
        module.SetupModule("registerSpace", _ => true).SetupVoid("dispose", _ => true).SetException(new JSDisconnectedException("gone"));
        var registration = await new KeyboardShortcuts(JSInterop.JSRuntime).RegisterSpaceAsync(() => Task.CompletedTask);

        await Should.NotThrowAsync(async () => await registration.DisposeAsync());
    }
}

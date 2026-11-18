using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using StemStudio.Web.Interop;
using StemStudio.Web.Practice;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Practice;

/// <summary>014 FR-019..FR-023: a live key map follows the shortcut preferences, also a change made while it registers.</summary>
public sealed class ShortcutKeysTests : IAsyncDisposable
{
    private readonly PreferencesState _preferences = new(new FakePreferencesStore(), new FakeWebInterop(), new FakeTimeProvider());
    private readonly IWebInterop _web = Substitute.For<IWebInterop>();
    private readonly IAsyncDisposable _registration = Substitute.For<IAsyncDisposable>();
    private readonly ShortcutKeys _keys;

    public ShortcutKeysTests() => _keys = new ShortcutKeys(_web, _preferences);

    public ValueTask DisposeAsync() => _preferences.DisposeAsync();

    private static ShortcutPreferences PedalsOn => UserPreferences.Default.Shortcuts with { PedalMode = true };

    private async Task<IAsyncDisposable> ChangeThenRegisterAsync()
    {
        await _preferences.SetShortcutsAsync(PedalsOn);
        return _registration;
    }

    [Fact]
    public async Task AChangeWhileRegistering_IsAppliedOnceTheRegistrationExists()
    {
        // E2E setlists scenario 3: the stored pedal preset loaded while the setlist scope registered; the map stayed without pedals.
#pragma warning disable CA2012 // NSubstitute arranges the ValueTask-returning call; it is never awaited here.
        _web.RegisterShortcutsAsync(ShortcutScope.Setlist, Arg.Any<KeyMap>(), Arg.Any<Func<string, Task>>())
            .Returns(_ => new ValueTask<IAsyncDisposable>(ChangeThenRegisterAsync()));
#pragma warning restore CA2012

        await using var live = await _keys.RegisterAsync(ShortcutScope.Setlist, _ => Task.CompletedTask);

        await _web.Received(1).UpdateShortcutsAsync(_registration, Arg.Is<KeyMap>(m => m.PedalMode));
    }

    [Fact]
    public async Task WithoutAChange_NoUpdateIsSent_AndLaterChangesAreFollowedUntilDisposed()
    {
#pragma warning disable CA2012 // NSubstitute arranges the ValueTask-returning call; it is never awaited here.
        _web.RegisterShortcutsAsync(ShortcutScope.Mixer, Arg.Any<KeyMap>(), Arg.Any<Func<string, Task>>())
            .Returns(new ValueTask<IAsyncDisposable>(_registration));
#pragma warning restore CA2012

        var live = await _keys.RegisterAsync(ShortcutScope.Mixer, _ => Task.CompletedTask);
        await _web.DidNotReceive().UpdateShortcutsAsync(Arg.Any<IAsyncDisposable>(), Arg.Any<KeyMap>());
        await _preferences.SetShortcutsAsync(PedalsOn);
        await live.DisposeAsync();
        await _preferences.SetShortcutsAsync(UserPreferences.Default.Shortcuts);

        await _web.Received(1).UpdateShortcutsAsync(_registration, Arg.Any<KeyMap>());
        await _registration.Received(1).DisposeAsync();
    }
}

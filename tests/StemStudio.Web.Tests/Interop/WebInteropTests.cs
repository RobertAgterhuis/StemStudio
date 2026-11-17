using System.Text.Json;
using Bunit;
using Microsoft.JSInterop;
using StemStudio.Web.Interop;

namespace StemStudio.Web.Tests.Interop;

public sealed class WebInteropTests : IAsyncDisposable
{
    private readonly BunitContext _context = new();
    private readonly BunitJSModuleInterop _module;
    private readonly WebInterop _interop;

    public WebInteropTests()
    {
        _module = _context.JSInterop.SetupModule(WebInterop.ModulePath);
        _module.Mode = JSRuntimeMode.Loose;
        _interop = new WebInterop(_context.JSInterop.JSRuntime);
    }

    public async ValueTask DisposeAsync()
    {
        await _interop.DisposeAsync();
        await _context.DisposeAsync();
    }

    private static JsonElement Record => JsonDocument.Parse("""{"schemaVersion":1,"level":"expert"}""").RootElement;

    [Fact]
    public async Task ReadPreferences_ImportsTheModuleOnce_AndReturnsTheRead()
    {
        var read = new StoredPreferencesRead(Record, true);
        _module.Setup<StoredPreferencesRead>("readPreferences").SetResult(read);

        var first = await _interop.ReadPreferencesAsync(Xunit.TestContext.Current.CancellationToken);
        await _interop.ReadPreferencesAsync(Xunit.TestContext.Current.CancellationToken);

        first.ShouldBe(read);
        _context.JSInterop.VerifyInvoke("import", calledTimes: 1)[0].Arguments.ShouldBe([WebInterop.ModulePath]);
    }

    [Fact]
    public async Task WriteAndApply_PassTheRecord()
    {
        _module.Setup<bool>("writePreferences", _ => true).SetResult(true);

        (await _interop.WritePreferencesAsync(Record, Xunit.TestContext.Current.CancellationToken)).ShouldBeTrue();
        await _interop.ApplyAppearanceAsync(Record, Xunit.TestContext.Current.CancellationToken);

        ((JsonElement)_module.VerifyInvoke("writePreferences").Arguments.Single()!).GetProperty("level").GetString().ShouldBe("expert");
        _module.VerifyInvoke("applyAppearance").Arguments.Single().ShouldBe(Record, new JsonElementComparer());
    }

    [Fact]
    public async Task WatchPreferences_PassesACallbackReceiver_AndDisposeReleasesIt()
    {
        var handle = _module.SetupModule("watchPreferences", _ => true);
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        string? received = null;

        var registration = await _interop.WatchPreferencesAsync(json =>
        {
            received = json;
            return Task.CompletedTask;
        });
        var receiver = (DotNetObjectReference<InteropCallbacks>)_module.VerifyInvoke("watchPreferences").Arguments.Single()!;
        await receiver.Value.OnPreferencesChanged("{}");
        await registration.DisposeAsync();

        received.ShouldBe("{}");
        handle.VerifyInvoke("dispose");
    }

    [Fact]
    public async Task WatchSystemAppearance_CallsBack()
    {
        _module.SetupModule("watchSystemAppearance", _ => true).Mode = JSRuntimeMode.Loose;
        var calls = 0;

        await using var registration = await _interop.WatchSystemAppearanceAsync(() =>
        {
            calls++;
            return Task.CompletedTask;
        });
        var receiver = (DotNetObjectReference<InteropCallbacks>)_module.VerifyInvoke("watchSystemAppearance").Arguments.Single()!;
        await receiver.Value.OnSystemAppearanceChanged();

        calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(ShortcutScope.Global, "global")]
    [InlineData(ShortcutScope.Mixer, "mixer")]
    public async Task RegisterShortcuts_SendsTheScopeName_AndForwardsCommands(ShortcutScope scope, string name)
    {
        _module.SetupModule("registerShortcuts", _ => true).Mode = JSRuntimeMode.Loose;
        string? command = null;

        await using var registration = await _interop.RegisterShortcutsAsync(scope, id =>
        {
            command = id;
            return Task.CompletedTask;
        });
        var args = _module.VerifyInvoke("registerShortcuts").Arguments;
        await ((DotNetObjectReference<InteropCallbacks>)args[0]!).Value.OnCommand("help.open");

        args[1].ShouldBe(name);
        command.ShouldBe("help.open");
    }

    [Fact]
    public async Task RegisterShortcuts_WithAKeyMap_SendsScopeAndMap_AndUpdateSwapsIt()
    {
        _module.SetupModule("registerShortcuts", _ => true).Mode = JSRuntimeMode.Loose;
        _module.SetupVoid("updateShortcuts", _ => true).SetVoidResult();
        var map = new StemStudio.Web.Practice.KeyMap([new StemStudio.Web.Practice.KeyBinding("mixer.stop", [new StemStudio.Web.Practice.KeyChordData("home")], "keys", "004")], true, false);

        await using var registration = await _interop.RegisterShortcutsAsync(ShortcutScope.Setlist, map, _ => Task.CompletedTask);
        await _interop.UpdateShortcutsAsync(registration, map with { SingleKey = false });
        await _interop.UpdateShortcutsAsync(new NoRegistration(), map);

        var args = _module.VerifyInvoke("registerShortcuts").Arguments;
        (args[1], args[2]).ShouldBe(("setlist", map));
        var update = _module.VerifyInvoke("updateShortcuts").Arguments;
        ((StemStudio.Web.Practice.KeyMap)update[1]!).SingleKey.ShouldBeFalse();
    }

    [Fact]
    public async Task CaptureChord_PassesTheTimeout_AndForwardsTheChord()
    {
        _module.SetupModule("captureChord", _ => true).Mode = JSRuntimeMode.Loose;
        StemStudio.Web.Practice.KeyChordData? captured = null;

        await using var registration = await _interop.CaptureChordAsync(TimeSpan.FromSeconds(10), chord =>
        {
            captured = chord;
            return Task.CompletedTask;
        });
        var args = _module.VerifyInvoke("captureChord").Arguments;
        await ((DotNetObjectReference<InteropCallbacks>)args[0]!).Value.OnChordCaptured(new StemStudio.Web.Practice.KeyChordData("j", Shift: false));

        args[1]!.ToString()!.ShouldContain("10000");
        captured.ShouldBe(new StemStudio.Web.Practice.KeyChordData("j", Shift: false));
    }

    private sealed class NoRegistration : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData("completed", true)]
    [InlineData("dismissed", false)]
    public async Task StartTour_PassesTheTour_AndMapsTheEnd(string status, bool completed)
    {
        _module.SetupModule("startTour", _ => true).Mode = JSRuntimeMode.Loose;
        var tour = new TourRuntime("import-basics", 1, false, [new("import-url", "Link", "Paste a link.")],
            new TourLabels("Next", "Back", "Done", "Close tour", "{{current}} of {{total}}"));
        TourEnded? ended = null;

        await using var registration = await _interop.StartTourAsync(tour, e =>
        {
            ended = e;
            return Task.CompletedTask;
        });
        var args = _module.VerifyInvoke("startTour").Arguments;
        await ((DotNetObjectReference<InteropCallbacks>)args[0]!).Value.OnTourEnded("import-basics", status, 3);

        args[1].ShouldBe(tour);
        ended.ShouldBe(new TourEnded("import-basics", completed, 3));
    }

    [Fact]
    public async Task FocusedHelpId_ReturnsTheMarker()
    {
        _module.Setup<string?>("focusedHelpId").SetResult("mixer-pan");

        (await _interop.FocusedHelpIdAsync(Xunit.TestContext.Current.CancellationToken)).ShouldBe("mixer-pan");
    }

    [Fact]
    public async Task CaptureFocus_DisposeRestoresFocus()
    {
        var handle = _module.SetupModule("captureFocus", _ => true);
        handle.SetupVoid("dispose", _ => true).SetVoidResult();

        var capture = await _interop.CaptureFocusAsync();
        await capture.DisposeAsync();

        handle.VerifyInvoke("dispose");
    }

    [Fact]
    public async Task Registration_DisposeAfterTheCircuitIsGone_DoesNotThrow()
    {
        var handle = _module.SetupModule("captureFocus", _ => true);
        handle.SetupVoid("dispose", _ => true).SetException(new JSDisconnectedException("gone"));

        var capture = await _interop.CaptureFocusAsync();

        await Should.NotThrowAsync(capture.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task Dispose_BeforeUse_AndAfterUse_IsSafe()
    {
        await _interop.DisposeAsync();
        _module.Setup<string?>("focusedHelpId").SetResult(null);
        var other = new WebInterop(_context.JSInterop.JSRuntime);
        await other.FocusedHelpIdAsync(Xunit.TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(other.DisposeAsync().AsTask());
    }

    private sealed class JsonElementComparer : IEqualityComparer<object?>
    {
        public new bool Equals(object? x, object? y) => ((JsonElement)x!).GetRawText() == ((JsonElement)y!).GetRawText();

        public int GetHashCode(object? obj) => 0;
    }
}

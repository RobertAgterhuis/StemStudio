using System.Text.Json;
using StemStudio.Web.Interop;
using StemStudio.Web.Practice;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>Recording fake of <see cref="IWebInterop"/>; tests trigger the JS callbacks through it.</summary>
public sealed class FakeWebInterop : IWebInterop
{
    private readonly Dictionary<ShortcutScope, Func<string, Task>> _commands = [];

    public StoredPreferencesRead ReadResult { get; set; } =
        new(JsonDocument.Parse("""{"schemaVersion":1}""").RootElement, true);

    public bool WriteResult { get; set; } = true;

    public List<JsonElement> Written { get; } = [];

    public List<JsonElement> Applied { get; } = [];

    public Func<string, Task>? PreferencesChanged { get; private set; }

    public Func<Task>? SystemChanged { get; private set; }

    public List<FakeRegistration> Registrations { get; } = [];

    public TourRuntime? StartedTour { get; private set; }

    public Func<TourEnded, Task>? TourEnded { get; private set; }

    public string? FocusedHelpId { get; set; }

    public int FocusCaptures { get; private set; }

    public int FocusRestores { get; private set; }

    public Exception? Failure { get; set; }

    public ValueTask<StoredPreferencesRead> ReadPreferencesAsync(CancellationToken cancellationToken = default) =>
        Failure is null ? ValueTask.FromResult(ReadResult) : ValueTask.FromException<StoredPreferencesRead>(Failure);

    public ValueTask<bool> WritePreferencesAsync(JsonElement preferences, CancellationToken cancellationToken = default)
    {
        Written.Add(preferences.Clone());
        return Failure is null ? ValueTask.FromResult(WriteResult) : ValueTask.FromException<bool>(Failure);
    }

    public ValueTask ApplyAppearanceAsync(JsonElement preferences, CancellationToken cancellationToken = default)
    {
        Applied.Add(preferences.Clone());
        return ValueTask.CompletedTask;
    }

    public ValueTask<IAsyncDisposable> WatchPreferencesAsync(Func<string, Task> onChanged)
    {
        PreferencesChanged = onChanged;
        return Register("preferences");
    }

    public ValueTask<IAsyncDisposable> WatchSystemAppearanceAsync(Func<Task> onChanged)
    {
        SystemChanged = onChanged;
        return Register("system");
    }

    public ValueTask<IAsyncDisposable> RegisterShortcutsAsync(ShortcutScope scope, Func<string, Task> onCommand)
    {
        _commands[scope] = onCommand;
        return Register($"shortcuts:{scope}");
    }

    /// <summary>The key map registered or last swapped in per scope (key map overload only).</summary>
    public Dictionary<ShortcutScope, KeyMap> KeyMaps { get; } = [];

    /// <summary>Number of <c>updateShortcuts</c> calls.</summary>
    public int KeyMapUpdates { get; private set; }

    /// <summary>The pending chord capture, if any.</summary>
    public Func<KeyChordData?, Task>? ChordCapture { get; private set; }

    /// <summary>Timeout of the last capture.</summary>
    public TimeSpan CaptureTimeout { get; private set; }

    public ValueTask<IAsyncDisposable> RegisterShortcutsAsync(ShortcutScope scope, KeyMap keyMap, Func<string, Task> onCommand)
    {
        _commands[scope] = onCommand;
        KeyMaps[scope] = keyMap;
        return Register($"shortcuts:{scope}");
    }

    public ValueTask UpdateShortcutsAsync(IAsyncDisposable registration, KeyMap keyMap)
    {
        KeyMapUpdates++;
        if (registration is FakeRegistration { Name: var name } && name.StartsWith("shortcuts:", StringComparison.Ordinal))
        {
            KeyMaps[Enum.Parse<ShortcutScope>(name["shortcuts:".Length..])] = keyMap;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<IAsyncDisposable> CaptureChordAsync(TimeSpan timeout, Func<KeyChordData?, Task> onCaptured)
    {
        ChordCapture = onCaptured;
        CaptureTimeout = timeout;
        return Register("capture");
    }

    /// <summary>Simulates the captured chord (null: Escape or timeout).</summary>
    public Task CaptureAsync(KeyChordData? chord) => ChordCapture!(chord);

    public ValueTask<IAsyncDisposable> StartTourAsync(TourRuntime tour, Func<TourEnded, Task> onEnded)
    {
        StartedTour = tour;
        TourEnded = onEnded;
        return Register($"tour:{tour.Id}");
    }

    public ValueTask<string?> FocusedHelpIdAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(FocusedHelpId);

    public ValueTask<IAsyncDisposable> CaptureFocusAsync()
    {
        FocusCaptures++;
        return ValueTask.FromResult<IAsyncDisposable>(new FakeRegistration("focus", () => FocusRestores++));
    }

    /// <summary>Simulates a key command from <c>registerShortcuts</c>.</summary>
    public Task CommandAsync(ShortcutScope scope, string commandId) => _commands[scope](commandId);

    public bool HasShortcuts(ShortcutScope scope) => _commands.ContainsKey(scope) && Registrations.Any(r => r.Name == $"shortcuts:{scope}" && !r.Disposed);

    public bool IsActive(string name) => Registrations.Any(r => r.Name == name && !r.Disposed);

    private ValueTask<IAsyncDisposable> Register(string name)
    {
        var registration = new FakeRegistration(name);
        Registrations.Add(registration);
        return ValueTask.FromResult<IAsyncDisposable>(registration);
    }

    public sealed class FakeRegistration(string name, Action? onDispose = null) : IAsyncDisposable
    {
        public string Name { get; } = name;

        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            onDispose?.Invoke();
            return ValueTask.CompletedTask;
        }
    }
}

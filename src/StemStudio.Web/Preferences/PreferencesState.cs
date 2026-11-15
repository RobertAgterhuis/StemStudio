using Microsoft.JSInterop;
using StemStudio.Web.Experience;
using StemStudio.Web.Interop;

namespace StemStudio.Web.Preferences;

/// <summary>
/// Scoped (per circuit) preferences state (data-model.md 3). Every change is applied to the page at once
/// (appearance attributes, one <see cref="Changed"/> event) and persisted after a 250 ms debounce; changes
/// from another tab are validated and applied without being written back (FR-010, FR-020).
/// </summary>
/// <param name="store">Persistence of the record.</param>
/// <param name="interop">Browser glue (appearance and watchers).</param>
/// <param name="time">Clock for the debounce and tour timestamps.</param>
public sealed class PreferencesState(IPreferencesStore store, IWebInterop interop, TimeProvider time) : IAsyncDisposable
{
    private readonly List<IAsyncDisposable> _watchers = [];
    private CancellationTokenSource? _save;
    private bool _loading;

    /// <summary>Debounce of the persisted write.</summary>
    public static TimeSpan SaveDelay { get; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Raised once after every applied change (load, setter, other tab).</summary>
    public event Action? Changed;

    /// <summary>The current preferences (defaults until loaded).</summary>
    public UserPreferences Current { get; private set; } = UserPreferences.Default;

    /// <summary>True once the stored record has been read.</summary>
    public bool Loaded { get; private set; }

    /// <summary>False when local storage cannot be used; choices then live only in memory.</summary>
    public bool StorageAvailable { get; private set; } = true;

    /// <summary>The scheduled write (completed when none is pending).</summary>
    public Task PendingSave { get; private set; } = Task.CompletedTask;

    /// <summary>Reads the record, applies it and starts watching other tabs and the OS (idempotent).</summary>
    /// <returns>A task that completes when loaded.</returns>
    public async Task LoadAsync()
    {
        if (Loaded || _loading)
        {
            return;
        }

        _loading = true;
        var snapshot = await store.LoadAsync();
        Current = snapshot.Preferences;
        StorageAvailable = snapshot.StorageAvailable;
        Loaded = true;
        await WatchAsync();
        await PublishAsync();
    }

    /// <summary>Sets the experience level.</summary>
    /// <param name="level">The level.</param>
    /// <returns>A task that completes when applied.</returns>
    public Task SetLevelAsync(ExperienceLevel level) => UpdateAsync(p => p with { Level = level });

    /// <summary>Sets the level from the welcome panel and dismisses it.</summary>
    /// <param name="level">The chosen level.</param>
    /// <returns>A task that completes when applied.</returns>
    public Task ChooseLevelAsync(ExperienceLevel level) =>
        UpdateAsync(p => p with { Level = level, Help = p.Help with { WelcomeDismissed = true } });

    /// <summary>Sets the theme.</summary>
    /// <param name="theme">The theme choice.</param>
    /// <returns>A task that completes when applied.</returns>
    public Task SetThemeAsync(ThemePreference theme) => UpdateAsync(p => p with { Theme = theme });

    /// <summary>Sets the density.</summary>
    /// <param name="density">The density choice.</param>
    /// <returns>A task that completes when applied.</returns>
    public Task SetDensityAsync(DensityPreference density) => UpdateAsync(p => p with { Density = density });

    /// <summary>Sets the motion choice.</summary>
    /// <param name="motion">The motion choice.</param>
    /// <returns>A task that completes when applied.</returns>
    public Task SetMotionAsync(MotionPreference motion) => UpdateAsync(p => p with { Motion = motion });

    /// <summary>Dismisses the welcome panel ("Not now" or a tour start).</summary>
    /// <returns>A task that completes when applied.</returns>
    public Task DismissWelcomeAsync() => UpdateAsync(p => p with { Help = p.Help with { WelcomeDismissed = true } });

    /// <summary>Records how a tour ended.</summary>
    /// <param name="tourId">The tour id.</param>
    /// <param name="version">The tour content version.</param>
    /// <param name="status">Completed or dismissed.</param>
    /// <returns>A task that completes when applied.</returns>
    public Task RecordTourAsync(string tourId, int version, TourStatus status)
    {
        var record = new TourRecord(version, status, time.GetUtcNow());
        return UpdateAsync(p => p with { Help = p.Help with { Tours = With(p.Help.Tours, tourId, record) } });
    }

    /// <summary>Hides an inline hint for good.</summary>
    /// <param name="topicId">The hint's topic id.</param>
    /// <returns>A task that completes when applied.</returns>
    public Task DismissHintAsync(string topicId) =>
        UpdateAsync(p => p with { Help = p.Help with { HintsDismissed = new HashSet<string>(p.Help.HintsDismissed, StringComparer.Ordinal) { topicId } } });

    /// <summary>Shows the welcome panel, tours and hints again.</summary>
    /// <returns>A task that completes when applied.</returns>
    public Task ResetHelpAsync() => UpdateAsync(p => p with { Help = HelpProgress.Empty });

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        CancelSave();
        foreach (var watcher in _watchers)
        {
            await watcher.DisposeAsync();
        }

        _watchers.Clear();
    }

    private static Dictionary<string, TourRecord> With(IReadOnlyDictionary<string, TourRecord> tours, string id, TourRecord record) =>
        new(tours, StringComparer.Ordinal) { [id] = record };

    private async Task UpdateAsync(Func<UserPreferences, UserPreferences> change)
    {
        Current = change(Current);
        await PublishAsync();
        ScheduleSave();
    }

    private async Task PublishAsync()
    {
        await ApplyAsync();
        Changed?.Invoke();
    }

    private async Task ApplyAsync()
    {
        try
        {
            await interop.ApplyAppearanceAsync(PreferencesJson.ToElement(Current));
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
            // Appearance is cosmetic; the rendered shell still follows the state.
        }
    }

    private async Task WatchAsync()
    {
        _watchers.Add(await interop.WatchPreferencesAsync(OnOtherTabAsync));
        _watchers.Add(await interop.WatchSystemAppearanceAsync(ApplyAsync));
    }

    private Task OnOtherTabAsync(string json)
    {
        if (!PreferencesJson.TryParse(json, out var incoming))
        {
            return Task.CompletedTask;
        }

        Current = incoming with { Help = incoming.Help with { Tours = MergeTours(Current.Help.Tours, incoming.Help.Tours) } };
        return PublishAsync();
    }

    private static Dictionary<string, TourRecord> MergeTours(
        IReadOnlyDictionary<string, TourRecord> mine, IReadOnlyDictionary<string, TourRecord> theirs)
    {
        var merged = new Dictionary<string, TourRecord>(theirs, StringComparer.Ordinal);
        foreach (var (id, record) in mine.Where(m => !theirs.TryGetValue(m.Key, out var other) || m.Value.At > other.At))
        {
            merged[id] = record;
        }

        return merged;
    }

    private void ScheduleSave()
    {
        CancelSave();
        if (!StorageAvailable)
        {
            return;
        }

        _save = new CancellationTokenSource();
        PendingSave = SaveLaterAsync(_save.Token);
    }

    private void CancelSave()
    {
        _save?.Cancel();
        _save?.Dispose();
        _save = null;
    }

    private async Task SaveLaterAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SaveDelay, time, cancellationToken);
            await store.SaveAsync(Current, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer change or the circuit ended.
        }
    }
}

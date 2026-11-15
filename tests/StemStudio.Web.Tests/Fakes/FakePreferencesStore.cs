using StemStudio.Web.Preferences;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>In-memory <see cref="IPreferencesStore"/> that records every save; <see cref="Hold"/> keeps the load pending.</summary>
public sealed class FakePreferencesStore : IPreferencesStore
{
    private TaskCompletionSource? _gate;

    public PreferencesSnapshot Snapshot { get; set; } = new(UserPreferences.Default, true);

    public List<UserPreferences> Saved { get; } = [];

    public int Loads { get; private set; }

    /// <summary>Makes the next load wait until <see cref="Release"/>.</summary>
    public void Hold() => _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Lets a held load complete.</summary>
    public void Release() => _gate?.TrySetResult();

    public async Task<PreferencesSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        Loads++;
        await (_gate?.Task ?? Task.CompletedTask);
        return Snapshot;
    }

    public Task<bool> SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default)
    {
        Saved.Add(preferences);
        return Task.FromResult(true);
    }
}

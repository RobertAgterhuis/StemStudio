using StemStudio.Web.Preferences;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>In-memory <see cref="IPreferencesStore"/> that records every save.</summary>
public sealed class FakePreferencesStore : IPreferencesStore
{
    public PreferencesSnapshot Snapshot { get; set; } = new(UserPreferences.Default, true);

    public List<UserPreferences> Saved { get; } = [];

    public int Loads { get; private set; }

    public Task<PreferencesSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        Loads++;
        return Task.FromResult(Snapshot);
    }

    public Task<bool> SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default)
    {
        Saved.Add(preferences);
        return Task.FromResult(true);
    }
}

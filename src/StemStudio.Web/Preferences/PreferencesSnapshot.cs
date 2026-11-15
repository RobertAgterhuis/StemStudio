namespace StemStudio.Web.Preferences;

/// <summary>Result of <see cref="IPreferencesStore.LoadAsync"/>.</summary>
/// <param name="Preferences">Validated preferences (defaults where the stored value was missing or invalid).</param>
/// <param name="StorageAvailable">False when browser storage threw; writes are then no-ops.</param>
public sealed record PreferencesSnapshot(UserPreferences Preferences, bool StorageAvailable);

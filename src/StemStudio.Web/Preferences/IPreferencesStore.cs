namespace StemStudio.Web.Preferences;

/// <summary>
/// Persists <see cref="UserPreferences"/> in browser local storage key <c>stemstudio.preferences</c>
/// (data-model.md 1). Implemented over <see cref="Interop.IWebInterop"/>; faked in unit tests.
/// </summary>
public interface IPreferencesStore
{
    /// <summary>Reads and validates the stored record; never throws for bad or missing data.</summary>
    /// <param name="cancellationToken">Cancels the interop call.</param>
    /// <returns>The preferences and whether storage is available.</returns>
    Task<PreferencesSnapshot> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes the whole record.</summary>
    /// <param name="preferences">Preferences to store.</param>
    /// <param name="cancellationToken">Cancels the interop call.</param>
    /// <returns>False when storage is unavailable or full, or the record exceeds 16 KB.</returns>
    Task<bool> SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default);
}

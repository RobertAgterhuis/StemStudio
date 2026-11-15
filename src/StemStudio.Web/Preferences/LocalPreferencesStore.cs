using Microsoft.JSInterop;
using StemStudio.Web.Interop;

namespace StemStudio.Web.Preferences;

/// <summary>
/// <see cref="IPreferencesStore"/> over browser local storage through <see cref="IWebInterop"/>. A failing
/// module (missing build output, circuit gone) behaves like unavailable storage: defaults in memory.
/// </summary>
/// <param name="interop">The web-interop module.</param>
public sealed class LocalPreferencesStore(IWebInterop interop) : IPreferencesStore
{
    /// <inheritdoc />
    public async Task<PreferencesSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var read = await interop.ReadPreferencesAsync(cancellationToken);
            return new PreferencesSnapshot(PreferencesJson.FromElement(read.Preferences), read.StorageAvailable);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
            return new PreferencesSnapshot(UserPreferences.Default, false);
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default)
    {
        try
        {
            return await interop.WritePreferencesAsync(PreferencesJson.ToElement(preferences), cancellationToken);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException)
        {
            return false;
        }
    }
}

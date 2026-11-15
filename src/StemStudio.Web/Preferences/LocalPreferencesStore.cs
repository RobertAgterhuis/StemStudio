using StemStudio.Web.Interop;

namespace StemStudio.Web.Preferences;

/// <summary>Store over interop. Red stub (T015).</summary>
/// <param name="interop">Interop.</param>
public sealed class LocalPreferencesStore(IWebInterop interop) : IPreferencesStore
{
    /// <inheritdoc />
    public Task<PreferencesSnapshot> LoadAsync(CancellationToken cancellationToken = default) =>
        interop is null ? Task.FromResult(new PreferencesSnapshot(UserPreferences.Default, true)) : throw new NotImplementedException();

    /// <inheritdoc />
    public Task<bool> SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}

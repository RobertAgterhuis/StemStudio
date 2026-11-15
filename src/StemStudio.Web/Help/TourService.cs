using StemStudio.Web.Interop;
using StemStudio.Web.Preferences;

namespace StemStudio.Web.Help;

/// <summary>Tours. Red stub (T040).</summary>
/// <param name="catalog">Catalog.</param>
/// <param name="interop">Interop.</param>
/// <param name="preferences">Preferences.</param>
public sealed class TourService(IHelpCatalog catalog, IWebInterop interop, PreferencesState preferences) : ITourLauncher, IAsyncDisposable
{
    /// <summary>Running.</summary>
    public string? Running => catalog is null || interop is null || preferences is null ? string.Empty : null;

    /// <inheritdoc />
    public Task StartAsync(string tourId) => Task.CompletedTask;

    /// <summary>Close.</summary>
    /// <returns>Task.</returns>
    public Task CloseAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

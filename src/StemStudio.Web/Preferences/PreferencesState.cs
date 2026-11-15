using StemStudio.Web.Experience;
using StemStudio.Web.Interop;

namespace StemStudio.Web.Preferences;

/// <summary>Scoped preferences state (data-model.md 3). Red stub (T015).</summary>
/// <param name="store">Store.</param>
/// <param name="interop">Interop.</param>
/// <param name="time">Clock.</param>
public sealed class PreferencesState(IPreferencesStore store, IWebInterop interop, TimeProvider time) : IAsyncDisposable
{
    /// <summary>Debounce.</summary>
    public static TimeSpan SaveDelay { get; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Raised on change.</summary>
    public event Action? Changed;

    /// <summary>Current.</summary>
    public UserPreferences Current { get; } = UserPreferences.Default;

    /// <summary>Loaded.</summary>
    public bool Loaded => Changed is null && store is null && interop is null && time is null;

    /// <summary>Storage.</summary>
    public bool StorageAvailable { get; }

    /// <summary>Pending save.</summary>
    public Task PendingSave => Task.CompletedTask;

    /// <summary>Load.</summary>
    /// <returns>Task.</returns>
    public Task LoadAsync() => throw new NotImplementedException();

    /// <summary>Level.</summary>
    /// <param name="level">Level.</param>
    /// <returns>Task.</returns>
    public Task SetLevelAsync(ExperienceLevel level) => throw new NotImplementedException();

    /// <summary>Choose.</summary>
    /// <param name="level">Level.</param>
    /// <returns>Task.</returns>
    public Task ChooseLevelAsync(ExperienceLevel level) => throw new NotImplementedException();

    /// <summary>Theme.</summary>
    /// <param name="theme">Theme.</param>
    /// <returns>Task.</returns>
    public Task SetThemeAsync(ThemePreference theme) => throw new NotImplementedException();

    /// <summary>Density.</summary>
    /// <param name="density">Density.</param>
    /// <returns>Task.</returns>
    public Task SetDensityAsync(DensityPreference density) => throw new NotImplementedException();

    /// <summary>Motion.</summary>
    /// <param name="motion">Motion.</param>
    /// <returns>Task.</returns>
    public Task SetMotionAsync(MotionPreference motion) => throw new NotImplementedException();

    /// <summary>Welcome.</summary>
    /// <returns>Task.</returns>
    public Task DismissWelcomeAsync() => throw new NotImplementedException();

    /// <summary>Tour.</summary>
    /// <param name="tourId">Id.</param>
    /// <param name="version">Version.</param>
    /// <param name="status">Status.</param>
    /// <returns>Task.</returns>
    public Task RecordTourAsync(string tourId, int version, TourStatus status) => throw new NotImplementedException();

    /// <summary>Hint.</summary>
    /// <param name="topicId">Topic.</param>
    /// <returns>Task.</returns>
    public Task DismissHintAsync(string topicId) => throw new NotImplementedException();

    /// <summary>Reset.</summary>
    /// <returns>Task.</returns>
    public Task ResetHelpAsync() => throw new NotImplementedException();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

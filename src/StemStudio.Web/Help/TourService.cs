using StemStudio.Web.Interop;
using StemStudio.Web.Preferences;

namespace StemStudio.Web.Help;

/// <summary>
/// Guided tours (FR-027, research.md R-07): resolves step texts from the help topics, runs the tour through
/// <see cref="IWebInterop.StartTourAsync"/> (driver.js) and records how it ended per tour version. One tour at a
/// time; starting another or closing (navigation) destroys the running one. Scoped per circuit.
/// </summary>
/// <param name="catalog">Help topics and tours.</param>
/// <param name="interop">Browser glue.</param>
/// <param name="preferences">Where tour results are recorded.</param>
public sealed class TourService(IHelpCatalog catalog, IWebInterop interop, PreferencesState preferences) : ITourLauncher, IAsyncDisposable
{
    private static readonly TourLabels Labels = new("Next", "Back", "Done", "Close tour", "{{current}} of {{total}}");

    private IAsyncDisposable? _running;

    /// <summary>Id of the running tour, or <see langword="null"/>.</summary>
    public string? Running { get; private set; }

    /// <inheritdoc />
    public async Task StartAsync(string tourId)
    {
        if (catalog.Tours.FirstOrDefault(t => t.Id == tourId) is not { } tour)
        {
            return;
        }

        await CloseAsync();
        Running = tour.Id;
        _running = await interop.StartTourAsync(Runtime(tour), ended => EndedAsync(tour, ended));
    }

    /// <summary>Destroys the running tour, if any (navigation, Escape from .NET).</summary>
    /// <returns>A task that completes when the tour is gone.</returns>
    public async Task CloseAsync()
    {
        var running = _running;
        _running = null;
        Running = null;
        await (running?.DisposeAsync() ?? ValueTask.CompletedTask);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await CloseAsync();

    private TourRuntime Runtime(TourDefinition tour)
    {
        var steps = tour.Steps.Select(Step).ToList();
        return new TourRuntime(tour.Id, tour.Version, preferences.Current.Motion == MotionPreference.Reduced, steps, Labels);
    }

    private TourRuntimeStep Step(TourStep step)
    {
        var topic = catalog.Topic(step.Topic);
        return new TourRuntimeStep(step.Target, step.Title ?? topic?.Title ?? string.Empty, step.Text ?? topic?.Summary ?? string.Empty);
    }

    private async Task EndedAsync(TourDefinition tour, TourEnded ended)
    {
        Running = null;
        _running = null;
        await preferences.RecordTourAsync(tour.Id, tour.Version, ended.Completed ? TourStatus.Completed : TourStatus.Dismissed);
    }
}

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using StemStudio.Web.Experience;
using StemStudio.Web.Interop;
using StemStudio.Web.Preferences;

namespace StemStudio.Web.Help;

/// <summary>
/// Guided tours (FR-027, research.md R-07, data-model.md 1.3): which tours are offered on a route and level,
/// starting one through <see cref="IWebInterop.StartTourAsync"/> (driver.js) with texts resolved from the help
/// topics, and recording how it ended per tour version. One tour at a time; starting another destroys the
/// running one; navigating away closes it and records it as dismissed. Scoped per circuit.
/// </summary>
public sealed class TourService : ITourLauncher, IAsyncDisposable
{
    private static readonly TourLabels Labels = new("Next", "Back", "Done", "Close tour", "{{current}} of {{total}}");

    private readonly IHelpCatalog _catalog;
    private readonly IWebInterop _interop;
    private readonly PreferencesState _preferences;
    private readonly NavigationManager _navigation;
    private IAsyncDisposable? _running;
    private TourDefinition? _runningTour;
    private bool _following;

    /// <summary>Creates the service.</summary>
    /// <param name="catalog">Help topics and tours.</param>
    /// <param name="interop">Browser glue.</param>
    /// <param name="preferences">Where tour results are recorded.</param>
    /// <param name="navigation">Navigation closes a running tour.</param>
    public TourService(IHelpCatalog catalog, IWebInterop interop, PreferencesState preferences, NavigationManager navigation)
    {
        _catalog = catalog;
        _interop = interop;
        _preferences = preferences;
        _navigation = navigation;
    }

    /// <summary>Id of the running tour, or <see langword="null"/>.</summary>
    public string? Running => _runningTour?.Id;

    /// <summary>The tours to offer: matching route, targeted level, and no record of this version yet.</summary>
    /// <param name="tours">All tours.</param>
    /// <param name="path">The current app path.</param>
    /// <param name="level">The effective level.</param>
    /// <param name="help">The help progress.</param>
    /// <returns>The tours in catalogue order.</returns>
    public static IReadOnlyList<TourDefinition> OffersFor(IEnumerable<TourDefinition> tours, string path, ExperienceLevel level, HelpProgress help) =>
        tours.Where(t => AppRoutes.Matches(t.Route, path) && t.Levels.Contains(level) && IsNew(t, help)).ToList();

    /// <summary>The tours to offer at the current location.</summary>
    /// <returns>The tours in catalogue order.</returns>
    public IReadOnlyList<TourDefinition> CurrentOffers() =>
        OffersFor(_catalog.Tours, AppRoutes.CurrentPath(_navigation), _preferences.Current.EffectiveLevel, _preferences.Current.Help);

    /// <inheritdoc />
    public async Task StartAsync(string tourId)
    {
        if (_catalog.Tours.FirstOrDefault(t => t.Id == tourId) is not { } tour)
        {
            return;
        }

        await CloseAsync();
        Follow();
        _runningTour = tour;
        _running = await _interop.StartTourAsync(Runtime(tour), ended => EndedAsync(tour, ended));
    }

    /// <summary>Records an offer dismissal for the tour's version (the offer disappears).</summary>
    /// <param name="tour">The offered tour.</param>
    /// <returns>A task that completes when recorded.</returns>
    public Task DismissOfferAsync(TourDefinition tour) => _preferences.RecordTourAsync(tour.Id, tour.Version, TourStatus.Dismissed);

    /// <summary>Destroys the running tour, if any, without recording it.</summary>
    /// <returns>A task that completes when the tour is gone.</returns>
    public async Task CloseAsync()
    {
        var running = _running;
        _running = null;
        _runningTour = null;
        await (running?.DisposeAsync() ?? ValueTask.CompletedTask);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_following)
        {
            _navigation.LocationChanged -= OnLocationChanged;
        }

        await CloseAsync();
    }

    private static bool IsNew(TourDefinition tour, HelpProgress help) =>
        !help.Tours.TryGetValue(tour.Id, out var record) || record.Version < tour.Version;

    private void Follow()
    {
        if (!_following)
        {
            _following = true;
            _navigation.LocationChanged += OnLocationChanged;
        }
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args) => _ = CloseOnNavigationAsync();

    private async Task CloseOnNavigationAsync()
    {
        if (_runningTour is { } tour)
        {
            await CloseAsync();
            await DismissOfferAsync(tour);
        }
    }

    private TourRuntime Runtime(TourDefinition tour)
    {
        var steps = tour.Steps.Select(Step).ToList();
        return new TourRuntime(tour.Id, tour.Version, _preferences.Current.Motion == MotionPreference.Reduced, steps, Labels);
    }

    private TourRuntimeStep Step(TourStep step)
    {
        var topic = _catalog.Topic(step.Topic);
        return new TourRuntimeStep(step.Target, step.Title ?? topic?.Title ?? string.Empty, step.Text ?? topic?.Summary ?? string.Empty);
    }

    private async Task EndedAsync(TourDefinition tour, TourEnded ended)
    {
        _runningTour = null;
        _running = null;
        await _preferences.RecordTourAsync(tour.Id, tour.Version, ended.Completed ? TourStatus.Completed : TourStatus.Dismissed);
    }
}

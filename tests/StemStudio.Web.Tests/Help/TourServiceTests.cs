using Microsoft.Extensions.Time.Testing;
using StemStudio.Web.Help;
using StemStudio.Web.Interop;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Help;

/// <summary>Starting tours and recording how they end (T055/T056; StartAsync pulled forward for the welcome panel, T040).</summary>
public sealed class TourServiceTests : IAsyncDisposable
{
    private readonly FakeWebInterop _interop = new();
    private readonly FakePreferencesStore _store = new();
    private readonly PreferencesState _preferences;
    private readonly TourService _tours;

    public TourServiceTests()
    {
        _preferences = new PreferencesState(_store, _interop, new FakeTimeProvider(new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero)));
        _tours = new TourService(new HelpCatalog(HelpSamples.Files), _interop, _preferences);
    }

    public async ValueTask DisposeAsync()
    {
        await _tours.DisposeAsync();
        await _preferences.DisposeAsync();
    }

    [Fact]
    public async Task Start_ResolvesTextsFromTopics_UnlessTheStepOverridesThem()
    {
        await _tours.StartAsync("mixer-basics");

        var tour = _interop.StartedTour!;
        tour.Id.ShouldBe("mixer-basics");
        tour.Version.ShouldBe(2);
        tour.Steps.ShouldBe(
        [
            new TourRuntimeStep("mixer-pan", "Pan", "Moves a stem between the left and right speaker."),
            new TourRuntimeStep("mixer-screen", "Mixer", "All stems."),
        ]);
        tour.Labels.ShouldBe(new TourLabels("Next", "Back", "Done", "Close tour", "{{current}} of {{total}}"));
        _tours.Running.ShouldBe("mixer-basics");
    }

    [Theory]
    [InlineData(MotionPreference.System, false)]
    [InlineData(MotionPreference.Reduced, true)]
    public async Task Start_PassesReducedMotion(MotionPreference motion, bool reduced)
    {
        _store.Snapshot = new(UserPreferences.Default with { Motion = motion }, true);
        await _preferences.LoadAsync();

        await _tours.StartAsync("mixer-basics");

        _interop.StartedTour!.ReducedMotion.ShouldBe(reduced);
    }

    [Fact]
    public async Task UnknownTour_DoesNothing()
    {
        await _tours.StartAsync("nope");

        _interop.StartedTour.ShouldBeNull();
        _tours.Running.ShouldBeNull();
    }

    [Theory]
    [InlineData(true, TourStatus.Completed)]
    [InlineData(false, TourStatus.Dismissed)]
    public async Task End_RecordsTheResultForTheTourVersion(bool completed, TourStatus status)
    {
        await _preferences.LoadAsync();
        await _tours.StartAsync("mixer-basics");

        await _interop.TourEnded!(new TourEnded("mixer-basics", completed, 1));

        _preferences.Current.Help.Tours["mixer-basics"].Version.ShouldBe(2);
        _preferences.Current.Help.Tours["mixer-basics"].Status.ShouldBe(status);
        _tours.Running.ShouldBeNull();
    }

    [Fact]
    public async Task StartingAnotherTour_DisposesTheRunningOne()
    {
        await _tours.StartAsync("mixer-basics");
        var first = _interop.Registrations.Single(r => r.Name == "tour:mixer-basics");

        await _tours.StartAsync("mixer-basics");

        first.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task Close_DestroysTheRunningTour()
    {
        await _tours.StartAsync("mixer-basics");

        await _tours.CloseAsync();

        _interop.IsActive("tour:mixer-basics").ShouldBeFalse();
        _tours.Running.ShouldBeNull();
        await Should.NotThrowAsync(() => _tours.CloseAsync());
    }
}

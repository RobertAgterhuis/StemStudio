using Microsoft.Extensions.Time.Testing;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Preferences;

public sealed class PreferencesStateTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);
    private readonly FakePreferencesStore _store = new();
    private readonly FakeWebInterop _interop = new();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly PreferencesState _state;
    private int _changes;

    public PreferencesStateTests()
    {
        _state = new PreferencesState(_store, _interop, _time);
        _state.Changed += () => _changes++;
    }

    public ValueTask DisposeAsync() => _state.DisposeAsync();

    private static UserPreferences With(ExperienceLevel? level, DensityPreference density = DensityPreference.Auto) =>
        UserPreferences.Default with { Level = level, Density = density };

    private async Task FlushAsync()
    {
        _time.Advance(PreferencesState.SaveDelay);
        await _state.PendingSave;
    }

    [Fact]
    public async Task Load_SetsCurrent_Loaded_AppliesAppearance_AndRaisesChangedOnce()
    {
        _store.Snapshot = new(With(ExperienceLevel.Expert), true);

        await _state.LoadAsync();

        _state.Loaded.ShouldBeTrue();
        _state.StorageAvailable.ShouldBeTrue();
        _state.Current.Level.ShouldBe(ExperienceLevel.Expert);
        _interop.Applied.Single().GetProperty("level").GetString().ShouldBe("expert");
        _interop.IsActive("preferences").ShouldBeTrue();
        _interop.IsActive("system").ShouldBeTrue();
        _changes.ShouldBe(1);
    }

    [Fact]
    public async Task Load_Twice_LoadsOnce()
    {
        await _state.LoadAsync();
        await _state.LoadAsync();

        _store.Loads.ShouldBe(1);
    }

    [Fact]
    public void BeforeLoad_NotLoaded_WithDefaults()
    {
        _state.Loaded.ShouldBeFalse();
        _state.Current.ShouldBe(UserPreferences.Default);
    }

    [Theory]
    [InlineData(null, DensityPreference.Auto, ExperienceLevel.Beginner, DensityPreference.Comfortable)]
    [InlineData(ExperienceLevel.Advanced, DensityPreference.Auto, ExperienceLevel.Advanced, DensityPreference.Comfortable)]
    [InlineData(ExperienceLevel.Expert, DensityPreference.Auto, ExperienceLevel.Expert, DensityPreference.Compact)]
    [InlineData(ExperienceLevel.Expert, DensityPreference.Comfortable, ExperienceLevel.Expert, DensityPreference.Comfortable)]
    public async Task EffectiveLevelAndDensity_FollowTheRecord(
        ExperienceLevel? level, DensityPreference density, ExperienceLevel effective, DensityPreference effectiveDensity)
    {
        _store.Snapshot = new(With(level, density), true);

        await _state.LoadAsync();

        _state.Current.EffectiveLevel.ShouldBe(effective);
        _state.Current.EffectiveDensity.ShouldBe(effectiveDensity);
    }

    [Fact]
    public async Task SetLevel_AppliesImmediately_RaisesChangedOnce_AndSavesAfterTheDebounce()
    {
        await _state.LoadAsync();
        _changes = 0;

        await _state.SetLevelAsync(ExperienceLevel.Expert);

        _changes.ShouldBe(1);
        _interop.Applied.Last().GetProperty("level").GetString().ShouldBe("expert");
        _store.Saved.ShouldBeEmpty();
        await FlushAsync();
        _store.Saved.Single().Level.ShouldBe(ExperienceLevel.Expert);
    }

    [Fact]
    public async Task SetShortcuts_IsSavedAtOnce_SoAReloadRightAfterTheChangeKeepsIt()
    {
        await _state.LoadAsync();
        var shortcuts = UserPreferences.Default.Shortcuts with { SingleKey = false };

        await _state.SetShortcutsAsync(shortcuts);

        _store.Saved.Single().Shortcuts.SingleKey.ShouldBeFalse();
        await FlushAsync();
        _store.Saved.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SetShortcuts_WithoutStorage_KeepsTheChangeInMemoryOnly()
    {
        _store.Snapshot = new(UserPreferences.Default, false);
        await _state.LoadAsync();

        await _state.SetShortcutsAsync(UserPreferences.Default.Shortcuts with { SingleKey = false });

        _state.Current.Shortcuts.SingleKey.ShouldBeFalse();
        _store.Saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetShortcuts_SupersedesAPendingDebouncedSave()
    {
        await _state.LoadAsync();
        await _state.SetThemeAsync(ThemePreference.Light);

        await _state.SetShortcutsAsync(UserPreferences.Default.Shortcuts with { SingleKey = false });
        await FlushAsync();

        var saved = _store.Saved.Single();
        saved.Theme.ShouldBe(ThemePreference.Light);
        saved.Shortcuts.SingleKey.ShouldBeFalse();
    }

    [Fact]
    public async Task QuickSuccessiveChanges_AreSavedOnce_WithTheLastValue()
    {
        await _state.LoadAsync();

        await _state.SetThemeAsync(ThemePreference.Light);
        _time.Advance(TimeSpan.FromMilliseconds(100));
        await _state.SetThemeAsync(ThemePreference.Dark);
        await FlushAsync();

        _store.Saved.Single().Theme.ShouldBe(ThemePreference.Dark);
    }

    [Fact]
    public async Task EachSetter_PersistsItsField()
    {
        await _state.LoadAsync();

        await _state.SetThemeAsync(ThemePreference.Light);
        await _state.SetDensityAsync(DensityPreference.Compact);
        await _state.SetMotionAsync(MotionPreference.Reduced);
        await FlushAsync();

        var saved = _store.Saved.Single();
        saved.Theme.ShouldBe(ThemePreference.Light);
        saved.Density.ShouldBe(DensityPreference.Compact);
        saved.Motion.ShouldBe(MotionPreference.Reduced);
        _changes.ShouldBe(4);
    }

    [Fact]
    public async Task ChooseLevel_SetsTheLevel_AndDismissesTheWelcome()
    {
        await _state.LoadAsync();

        await _state.ChooseLevelAsync(ExperienceLevel.Beginner);

        _state.Current.Level.ShouldBe(ExperienceLevel.Beginner);
        _state.Current.Help.WelcomeDismissed.ShouldBeTrue();
    }

    [Fact]
    public async Task DismissWelcome_KeepsTheLevelUnchosen()
    {
        await _state.LoadAsync();

        await _state.DismissWelcomeAsync();

        _state.Current.Help.WelcomeDismissed.ShouldBeTrue();
        _state.Current.Level.ShouldBeNull();
    }

    [Fact]
    public async Task RecordTour_StoresVersionStatusAndTime()
    {
        await _state.LoadAsync();

        await _state.RecordTourAsync("import-basics", 2, TourStatus.Completed);
        await FlushAsync();

        _store.Saved.Single().Help.Tours["import-basics"].ShouldBe(new TourRecord(2, TourStatus.Completed, Now));
    }

    [Fact]
    public async Task DismissHint_ThenResetHelp_ClearsWelcomeToursAndHints()
    {
        await _state.LoadAsync();
        await _state.DismissHintAsync("mixer-pan");
        await _state.DismissWelcomeAsync();
        await _state.RecordTourAsync("import-basics", 1, TourStatus.Dismissed);
        _state.Current.Help.HintsDismissed.ShouldContain("mixer-pan");

        await _state.ResetHelpAsync();

        _state.Current.Help.WelcomeDismissed.ShouldBeFalse();
        _state.Current.Help.Tours.ShouldBeEmpty();
        _state.Current.Help.HintsDismissed.ShouldBeEmpty();
    }

    [Fact]
    public async Task StorageUnavailable_IsReported_AndNothingIsSaved()
    {
        _store.Snapshot = new(UserPreferences.Default, false);
        await _state.LoadAsync();

        await _state.SetLevelAsync(ExperienceLevel.Advanced);
        await FlushAsync();

        _state.StorageAvailable.ShouldBeFalse();
        _state.Current.Level.ShouldBe(ExperienceLevel.Advanced);
        _store.Saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task CrossTab_ValidRecord_IsApplied_WithoutSaving()
    {
        await _state.LoadAsync();
        _changes = 0;

        await _interop.PreferencesChanged!("""{"schemaVersion":1,"level":"expert","theme":"dark","density":"auto","motion":"system","help":{"welcomeDismissed":true,"tours":{},"hintsDismissed":[]}}""");

        _state.Current.Level.ShouldBe(ExperienceLevel.Expert);
        _state.Current.Theme.ShouldBe(ThemePreference.Dark);
        _interop.Applied.Last().GetProperty("theme").GetString().ShouldBe("dark");
        _changes.ShouldBe(1);
        await FlushAsync();
        _store.Saved.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("""{"schemaVersion":3,"level":"expert"}""")]
    [InlineData("[]")]
    public async Task CrossTab_InvalidRecord_IsIgnored(string json)
    {
        await _state.LoadAsync();
        _changes = 0;

        await _interop.PreferencesChanged!(json);

        _state.Current.ShouldBe(UserPreferences.Default);
        _changes.ShouldBe(0);
    }

    [Fact]
    public async Task CrossTab_MergesTours_ByTheNewerTime()
    {
        await _state.LoadAsync();
        await _state.RecordTourAsync("import-basics", 1, TourStatus.Completed);
        await _state.RecordTourAsync("mixer-basics", 1, TourStatus.Dismissed);

        await _interop.PreferencesChanged!("""
            {"schemaVersion":1,"level":null,"theme":"system","density":"auto","motion":"system",
             "help":{"welcomeDismissed":false,"hintsDismissed":[],"tours":{
               "import-basics":{"version":1,"status":"dismissed","at":"2026-11-15T09:00:00Z"},
               "mixer-basics":{"version":2,"status":"completed","at":"2026-11-15T11:00:00Z"},
               "track-basics":{"version":1,"status":"completed","at":"2026-11-15T08:00:00Z"}}}}
            """);

        var tours = _state.Current.Help.Tours;
        tours["import-basics"].Status.ShouldBe(TourStatus.Completed);
        tours["mixer-basics"].Version.ShouldBe(2);
        tours.ShouldContainKey("track-basics");
    }

    [Fact]
    public async Task SystemAppearanceChange_ReappliesTheAppearance()
    {
        await _state.LoadAsync();
        var before = _interop.Applied.Count;

        await _interop.SystemChanged!();

        _interop.Applied.Count.ShouldBe(before + 1);
    }

    [Fact]
    public async Task Dispose_RemovesWatchers_AndCancelsThePendingSave()
    {
        await _state.LoadAsync();
        await _state.SetLevelAsync(ExperienceLevel.Expert);

        await _state.DisposeAsync();
        _time.Advance(PreferencesState.SaveDelay);
        await _state.PendingSave;

        _interop.IsActive("preferences").ShouldBeFalse();
        _interop.IsActive("system").ShouldBeFalse();
        _store.Saved.ShouldBeEmpty();
    }

    [Fact]
    public async Task Dispose_BeforeLoad_IsSafe()
    {
        await _state.DisposeAsync();

        _state.Loaded.ShouldBeFalse();
    }
}

using System.Text.Json;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;

namespace StemStudio.Web.Tests.Preferences;

public sealed class PreferencesJsonTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 15, 10, 12, 0, TimeSpan.Zero);

    private static readonly UserPreferences Sample = new(
        ExperienceLevel.Expert,
        ThemePreference.Dark,
        DensityPreference.Compact,
        MotionPreference.Reduced,
        new HelpProgress(
            true,
            new Dictionary<string, TourRecord> { ["import-basics"] = new(1, TourStatus.Completed, At) },
            new HashSet<string> { "mixer-pan" }));

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void ToElement_WritesTheStoredRecordShape()
    {
        var json = PreferencesJson.ToElement(Sample).GetRawText();

        json.ShouldBe("""{"schemaVersion":1,"level":"expert","theme":"dark","density":"compact","motion":"reduced","help":{"welcomeDismissed":true,"tours":{"import-basics":{"version":1,"status":"completed","at":"2026-11-15T10:12:00Z"}},"hintsDismissed":["mixer-pan"]}}""");
    }

    [Fact]
    public void ToElement_WritesNullForAnUnchosenLevel_AndDismissedTours()
    {
        var prefs = UserPreferences.Default with
        {
            Help = HelpProgress.Empty with
            {
                Tours = new Dictionary<string, TourRecord> { ["t"] = new(3, TourStatus.Dismissed, At) },
            },
        };

        var element = PreferencesJson.ToElement(prefs);

        element.GetProperty("level").ValueKind.ShouldBe(JsonValueKind.Null);
        element.GetProperty("help").GetProperty("tours").GetProperty("t").GetProperty("status").GetString().ShouldBe("dismissed");
    }

    [Fact]
    public void FromElement_RoundTrips()
    {
        var back = PreferencesJson.FromElement(PreferencesJson.ToElement(Sample));

        back.Level.ShouldBe(Sample.Level);
        back.Theme.ShouldBe(Sample.Theme);
        back.Density.ShouldBe(Sample.Density);
        back.Motion.ShouldBe(Sample.Motion);
        back.Help.WelcomeDismissed.ShouldBeTrue();
        back.Help.Tours["import-basics"].ShouldBe(new TourRecord(1, TourStatus.Completed, At));
        back.Help.HintsDismissed.ShouldBe(["mixer-pan"]);
    }

    [Fact]
    public void FromElement_ReplacesInvalidFieldsByDefaults()
    {
        var back = PreferencesJson.FromElement(Parse("""
            {"schemaVersion":1,"level":"guru","theme":3,"density":"tiny","motion":null,
             "help":{"welcomeDismissed":"yes","tours":{"a":{"version":0,"status":"completed","at":"2026-11-15T10:12:00Z"},
               "b":{"version":1,"status":"started","at":"2026-11-15T10:12:00Z"},"c":{"version":1,"status":"completed","at":"soon"},
               "d":"x"},"hintsDismissed":[1,"ok"]}}
            """));

        back.Level.ShouldBeNull();
        back.Theme.ShouldBe(ThemePreference.System);
        back.Density.ShouldBe(DensityPreference.Auto);
        back.Motion.ShouldBe(MotionPreference.System);
        back.Help.WelcomeDismissed.ShouldBeFalse();
        back.Help.Tours.ShouldBeEmpty();
        back.Help.HintsDismissed.ShouldBe(["ok"]);
    }

    [Theory]
    [InlineData("""{"schemaVersion":1}""")]
    [InlineData("""{"schemaVersion":1,"help":[]}""")]
    [InlineData("""{"schemaVersion":1,"help":{"tours":[],"hintsDismissed":{}}}""")]
    [InlineData("[]")]
    public void FromElement_MissingOrWrongShapes_GiveDefaults(string json)
    {
        PreferencesJson.FromElement(Parse(json)).ShouldBe(UserPreferences.Default, new PreferencesComparer());
    }

    [Theory]
    [InlineData("""{"schemaVersion":1,"level":"advanced"}""", true)]
    [InlineData("""{"schemaVersion":2,"level":"advanced"}""", false)]
    [InlineData("""{"level":"advanced"}""", false)]
    [InlineData("[]", false)]
    [InlineData("{broken", false)]
    public void TryParse_AcceptsOnlySchemaVersionOneObjects(string json, bool valid)
    {
        PreferencesJson.TryParse(json, out var prefs).ShouldBe(valid);
        (prefs is not null).ShouldBe(valid);
    }

    private sealed class PreferencesComparer : IEqualityComparer<UserPreferences>
    {
        public bool Equals(UserPreferences? x, UserPreferences? y) =>
            x!.Level == y!.Level && x.Theme == y.Theme && x.Density == y.Density && x.Motion == y.Motion
            && x.Help.WelcomeDismissed == y.Help.WelcomeDismissed && x.Help.Tours.Count == y.Help.Tours.Count
            && x.Help.HintsDismissed.Count == y.Help.HintsDismissed.Count;

        public int GetHashCode(UserPreferences obj) => 0;
    }
}

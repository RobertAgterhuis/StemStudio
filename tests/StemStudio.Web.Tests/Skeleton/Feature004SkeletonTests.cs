using System.Text.Json;
using StemStudio.TestDoubles.JsonSchema;
using StemStudio.Web.Experience;
using StemStudio.Web.Help;
using StemStudio.Web.Interop;
using StemStudio.Web.Preferences;

namespace StemStudio.Web.Tests.Skeleton;

/// <summary>Phase 0 skeleton of feature 004 (T002/T003): stub types, the embedded topic and the help schemas.</summary>
public sealed class Feature004SkeletonTests
{
    private const string WelcomeResource = "StemStudio.Web.Help.topics.start.welcome.md";

    private static readonly SchemaSubsetValidator TopicSchema = LoadSchema("help-topic.schema.json");
    private static readonly SchemaSubsetValidator TourSchema = LoadSchema("tour.schema.json");

    [Fact]
    public void ExperienceLevels_AreOrderedBeginnerAdvancedExpert()
    {
        Enum.GetValues<ExperienceLevel>().ShouldBe([ExperienceLevel.Beginner, ExperienceLevel.Advanced, ExperienceLevel.Expert]);
        new UiFeature("mixer.pan", ExperienceLevel.Advanced, null, "mixer-pan").Max.ShouldBeNull();
    }

    [Fact]
    public void DefaultPreferences_MatchDataModel()
    {
        var defaults = UserPreferences.Default;

        defaults.Level.ShouldBeNull();
        defaults.EffectiveLevel.ShouldBe(ExperienceLevel.Beginner);
        defaults.Theme.ShouldBe(ThemePreference.System);
        defaults.Motion.ShouldBe(MotionPreference.System);
        defaults.Help.ShouldBe(HelpProgress.Empty);
        defaults.Help.Tours.ShouldBeEmpty();
        defaults.Help.HintsDismissed.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null, DensityPreference.Auto, DensityPreference.Comfortable)]
    [InlineData(ExperienceLevel.Advanced, DensityPreference.Auto, DensityPreference.Comfortable)]
    [InlineData(ExperienceLevel.Expert, DensityPreference.Auto, DensityPreference.Compact)]
    [InlineData(ExperienceLevel.Expert, DensityPreference.Comfortable, DensityPreference.Comfortable)]
    [InlineData(ExperienceLevel.Beginner, DensityPreference.Compact, DensityPreference.Compact)]
    public void EffectiveDensity_ResolvesAutoFromLevel(ExperienceLevel? level, DensityPreference density, DensityPreference expected)
    {
        var preferences = UserPreferences.Default with { Level = level, Density = density };

        preferences.EffectiveDensity.ShouldBe(expected);
    }

    [Fact]
    public void StubRecords_CarryTheirValues()
    {
        var at = new DateTimeOffset(2026, 11, 15, 10, 12, 0, TimeSpan.Zero);
        var snapshot = new PreferencesSnapshot(UserPreferences.Default, StorageAvailable: false);
        var record = new TourRecord(1, TourStatus.Completed, at);
        var tour = new TourDefinition("import-basics", 1, "Import basics", "/", [ExperienceLevel.Beginner], [new TourStep("import-url", "import-url")]);
        var runtime = new TourRuntime("import-basics", 1, false, [new TourRuntimeStep("import-url", "Link", "Paste a link.")], new TourLabels("Next", "Previous", "Done", "Close", "{{current}} of {{total}}"));

        snapshot.StorageAvailable.ShouldBeFalse();
        record.Status.ShouldBe(TourStatus.Completed);
        tour.Steps[0].Title.ShouldBeNull();
        runtime.Steps.ShouldHaveSingleItem().Target.ShouldBe("import-url");
        new TourEnded("import-basics", true, 4).LastStep.ShouldBe(4);
        new HelpHit("welcome", 1.5).Score.ShouldBe(1.5);
        new StoredPreferencesRead(JsonSerializer.SerializeToElement(new { schemaVersion = 1 }), true).StorageAvailable.ShouldBeTrue();
    }

    [Fact]
    public void HelpTopicAndRenderedTopic_DefaultOptionalListsToEmpty()
    {
        var topic = new HelpTopic { Id = "welcome", Title = "Welcome", Area = "start", Summary = "Hi.", Reviewed = "004", Markdown = "Text" };
        var rendered = new RenderedTopic(topic, "<p>Text</p>", [new HelpHeading("start", "Start", 2)]);

        topic.Levels.ShouldBeEmpty();
        topic.Anchors.ShouldBeEmpty();
        topic.Keywords.ShouldBeEmpty();
        topic.Related.ShouldBeEmpty();
        rendered.Headings.ShouldHaveSingleItem().Level.ShouldBe(2);
    }

    [Theory]
    [InlineData(typeof(IPreferencesStore))]
    [InlineData(typeof(IHelpCatalog))]
    [InlineData(typeof(IHelpSearch))]
    [InlineData(typeof(IWebInterop))]
    public void ServiceContracts_AreInterfaces(Type contract) => contract.IsInterface.ShouldBeTrue();

    [Fact]
    public void ShortcutScopes_AreGlobalAndMixer() =>
        Enum.GetNames<ShortcutScope>().ShouldBe(["Global", "Mixer", "Library", "Setlist"]);

    [Fact]
    public void WelcomeTopic_IsEmbeddedAndValidatesAgainstTheTopicSchema()
    {
        var markdown = ReadEmbedded(WelcomeResource);

        var frontMatter = FrontMatterJson.FromTopic(markdown);

        TopicSchema.Validate(frontMatter).ShouldBeEmpty();
        frontMatter.GetProperty("id").GetString().ShouldBe("welcome");
    }

    [Fact]
    public void TopicSchema_RejectsContractViolations()
    {
        var frontMatter = FrontMatterJson.FromYaml(
            "id: Bad_Id\ntitle: ''\narea: studio\nsummary: x\nreviewed: 4\nlevels: [beginner, beginner]\nroute: null\nextra: 1");

        var errors = TopicSchema.Validate(frontMatter);

        errors.ShouldContain(e => e.StartsWith("$.id:", StringComparison.Ordinal));
        errors.ShouldContain(e => e.StartsWith("$.title:", StringComparison.Ordinal));
        errors.ShouldContain(e => e.StartsWith("$.area:", StringComparison.Ordinal));
        errors.ShouldContain(e => e.StartsWith("$.reviewed:", StringComparison.Ordinal));
        errors.ShouldContain(e => e.StartsWith("$.levels:", StringComparison.Ordinal));
        errors.ShouldContain("$: property 'extra' is not allowed");
        errors.ShouldNotContain(e => e.StartsWith("$.route:", StringComparison.Ordinal));
    }

    private const string ValidTour = """
        { "id": "mixer-basics", "version": 1, "title": "Mixer basics", "route": "/tracks/{id}/mixer",
          "levels": ["beginner"],
          "steps": [ { "target": "mixer-presets", "topic": "mixer-presets" },
                     { "target": "mixer-volume", "topic": "mixer-volume", "text": "Drag a fader or use the arrow keys." } ] }
        """;

    private const string InvalidTour = """{ "id": "x", "version": 0, "title": "T", "route": "tracks", "levels": [], "steps": [ { "target": "a" } ] }""";

    [Fact]
    public void TourSchema_AcceptsTheContractExample() =>
        TourSchema.Validate(Parse(ValidTour)).ShouldBeEmpty();

    [Fact]
    public void TourSchema_RejectsContractViolations()
    {
        TourSchema.Validate(Parse(InvalidTour)).ShouldBe(
        [
            "$.version: 0 is below minimum 1",
            "$.route: 'tracks' does not match ^(\\*|/.*)$",
            "$.levels: 0 items violate minItems 1",
            "$.steps[0]: required property 'topic' is missing",
        ],
        ignoreOrder: true);
    }

    [Fact]
    public void SubsetValidator_RefusesUnknownKeywords()
    {
        var path = Path.Combine(Path.GetTempPath(), $"schema-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "type": "object", "oneOf": [] }""");
        try
        {
            Should.Throw<NotSupportedException>(() => SchemaSubsetValidator.Load(path).Validate(Parse("{}")));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FrontMatter_WithoutFence_IsRejected()
    {
        Should.Throw<FormatException>(() => FrontMatterJson.FromTopic("id: welcome"));
        Should.Throw<FormatException>(() => FrontMatterJson.FromTopic("---\nid: welcome"));
    }

    private static SchemaSubsetValidator LoadSchema(string fileName) =>
        SchemaSubsetValidator.Load(Path.Combine(AppContext.BaseDirectory, "Contracts", fileName));

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static string ReadEmbedded(string name)
    {
        using var stream = typeof(UserPreferences).Assembly.GetManifestResourceStream(name);
        stream.ShouldNotBeNull($"embedded resource {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

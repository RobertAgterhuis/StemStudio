using System.Text.Json;
using StemStudio.TestDoubles.JsonSchema;
using StemStudio.Web.Experience;
using StemStudio.Web.Help;
using StemStudio.Web.Tests.Skeleton;
using static StemStudio.Web.Tests.Help.HelpSamples;

namespace StemStudio.Web.Tests.Help;

public sealed class HelpCatalogTests
{
    private static readonly SchemaSubsetValidator TopicSchema =
        SchemaSubsetValidator.Load(Path.Combine(AppContext.BaseDirectory, "Contracts", "help-topic.schema.json"));

    private static readonly SchemaSubsetValidator TourSchema =
        SchemaSubsetValidator.Load(Path.Combine(AppContext.BaseDirectory, "Contracts", "tour.schema.json"));

    private static HelpCatalog Sample() => new(Files);

    private static string LoadError(params HelpFile[] files) =>
        Should.Throw<HelpContentException>(() => new HelpCatalog(files)).Message;

    [Fact]
    public void Loads_TopicsWithTheirFrontMatter()
    {
        var pan = Sample().Topic("mixer-pan")!;

        pan.Title.ShouldBe("Pan");
        pan.Area.ShouldBe("mixer");
        pan.Feature.ShouldBe("mixer.pan");
        pan.Levels.ShouldBe([ExperienceLevel.Advanced, ExperienceLevel.Expert]);
        pan.Anchors.ShouldBe(["mixer-pan"]);
        pan.Keywords.ShouldBe(["stereo", "balance"]);
        pan.Related.ShouldBe(["mixer-screen"]);
        pan.Reviewed.ShouldBe("004");
        pan.Markdown.Trim().ShouldStartWith("Pan places a stem");
        Sample().Topic("nope").ShouldBeNull();
    }

    [Fact]
    public void ScreenTopic_IsFoundByRouteTemplate()
    {
        Sample().ScreenTopic("/tracks/{id}/mixer")!.Id.ShouldBe("mixer-screen");
        Sample().ScreenTopic("/nowhere").ShouldBeNull();
    }

    [Theory]
    [InlineData("mixer-pan", "mixer-pan")]
    [InlineData("strip-drums", "mixer-screen")]
    [InlineData("strip-vocals", "welcome")]
    [InlineData("mixer-screen", "mixer-screen")]
    public void TopicForAnchor_PrefersLiteralAnchors_OverPatterns(string helpId, string topic) =>
        Sample().TopicForAnchor(helpId)!.Id.ShouldBe(topic);

    [Fact]
    public void TopicForAnchor_Unknown_IsNull() => Sample().TopicForAnchor("nothing-here").ShouldBeNull();

    [Fact]
    public void Areas_AreListedInContractOrder_AndOnlyWhenUsed() =>
        Sample().Areas.ShouldBe(["start", "mixer", "glossary"]);

    [Fact]
    public void Tours_AreLoaded()
    {
        var tour = Sample().Tours.Single();

        tour.Id.ShouldBe("mixer-basics");
        tour.Version.ShouldBe(2);
        tour.Route.ShouldBe("/tracks/{id}/mixer");
        tour.Levels.ShouldBe([ExperienceLevel.Beginner]);
        tour.Steps[1].ShouldBe(new TourStep("mixer-screen", "mixer-screen", "Mixer", "All stems."));
    }

    [Fact]
    public void DuplicateId_FailsWithAClearMessage() =>
        LoadError(TopicFile("stem", "glossary"), TopicFile("stem", "mixer")).ShouldContain("duplicate topic id 'stem'");

    [Fact]
    public void FileNameMismatch_Fails() =>
        LoadError(new HelpFile("topics/mixer/other.md", Topic("mixer-pan"))).ShouldContain("does not match the file name 'other'");

    [Fact]
    public void AreaFolderMismatch_Fails() =>
        LoadError(new HelpFile("topics/glossary/mixer-pan.md", Topic("mixer-pan"))).ShouldContain("area 'mixer' does not match the folder 'glossary'");

    [Fact]
    public void LongSummary_Fails() =>
        LoadError(TopicFile("x", content: Topic("x", new(Summary: new string('a', 141))))).ShouldContain("summary is longer than 140");

    [Fact]
    public void MissingRequiredField_Fails() =>
        LoadError(TopicFile("x", content: "---\nid: x\narea: mixer\nsummary: s\nreviewed: \"004\"\n---\nBody")).ShouldContain("title");

    [Fact]
    public void InvalidId_Fails() =>
        LoadError(new HelpFile("topics/mixer/Bad_Id.md", Topic("Bad_Id"))).ShouldContain("invalid topic id 'Bad_Id'");

    [Fact]
    public void UnknownLevel_Fails() =>
        LoadError(TopicFile("x", content: Topic("x", new(Extra: "levels: [guru]")))).ShouldContain("unknown level 'guru'");

    [Fact]
    public void MissingFrontMatter_Fails() =>
        LoadError(TopicFile("x", content: "Just text")).ShouldContain("front matter");

    [Fact]
    public void BrokenRelatedLink_Fails() =>
        LoadError(TopicFile("x", content: Topic("x", new(Extra: "related: [nope]")))).ShouldContain("related topic 'nope' does not exist");

    [Fact]
    public void BrokenHelpLink_Fails() =>
        LoadError(TopicFile("x", content: Topic("x", new(Body: "See [this](help:nope).")))).ShouldContain("link to unknown topic 'nope'");

    [Fact]
    public void TourWithUnknownTopic_Fails() =>
        LoadError(TopicFile("stem", "glossary"), TourFile("t", """{"id":"t","version":1,"title":"T","route":"*","levels":["expert"],"steps":[{"target":"a","topic":"nope"}]}"""))
            .ShouldContain("step 1 uses unknown topic 'nope'");

    [Fact]
    public void TourFileNameMismatch_Fails() =>
        LoadError(TopicFile("stem", "glossary"), TourFile("t", """{"id":"u","version":1,"title":"T","route":"*","levels":["expert"],"steps":[{"target":"a","topic":"stem"}]}"""))
            .ShouldContain("does not match the file name 't'");

    [Theory]
    [InlineData("""{"id":"t","version":0,"title":"T","route":"*","levels":["expert"],"steps":[{"target":"a","topic":"stem"}]}""", "version")]
    [InlineData("""{"id":"t","version":1,"title":"T","route":"*","levels":[],"steps":[{"target":"a","topic":"stem"}]}""", "levels")]
    [InlineData("""{"id":"t","version":1,"title":"T","route":"*","levels":["expert"],"steps":[]}""", "steps")]
    [InlineData("""{"id":"t","version":1""", "invalid JSON")]
    public void InvalidTour_Fails(string json, string message) =>
        LoadError(TopicFile("stem", "glossary"), TourFile("t", json)).ShouldContain(message);

    [Fact]
    public void UnknownFile_Fails() => LoadError(new HelpFile("other/readme.txt", "x")).ShouldContain("unexpected help file");

    [Fact]
    public void FromAssembly_LoadsTheEmbeddedContent()
    {
        var catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

        catalog.Topic("welcome")!.Area.ShouldBe("start");
    }

    [Fact]
    public void ResourcePath_MapsManifestNames()
    {
        HelpCatalog.ResourcePath("StemStudio.Web.Help.topics.mixer.mixer-pan.md").ShouldBe("topics/mixer/mixer-pan.md");
        HelpCatalog.ResourcePath("StemStudio.Web.Help.tours.mixer-basics.json").ShouldBe("tours/mixer-basics.json");
        HelpCatalog.ResourcePath("StemStudio.Web.Other.thing.txt").ShouldBeNull();
    }

    [Fact]
    public void EveryEmbeddedTopicAndTour_ValidatesAgainstItsSchema()
    {
        foreach (var file in HelpCatalog.EmbeddedFiles(typeof(HelpCatalog).Assembly))
        {
            var (schema, instance) = file.Path.StartsWith("topics/", StringComparison.Ordinal)
                ? (TopicSchema, FrontMatterJson.FromTopic(file.Content))
                : (TourSchema, JsonDocument.Parse(file.Content).RootElement);
            schema.Validate(instance).ShouldBeEmpty(file.Path);
        }
    }

    [Fact]
    public void SampleContent_ValidatesAgainstTheSchemas()
    {
        foreach (var file in Files.Where(f => f.Path.EndsWith(".md", StringComparison.Ordinal)))
        {
            TopicSchema.Validate(FrontMatterJson.FromTopic(file.Content)).ShouldBeEmpty(file.Path);
        }

        TourSchema.Validate(JsonDocument.Parse(Files[^1].Content).RootElement).ShouldBeEmpty();
    }
}

using StemStudio.Web.Help;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>
/// Help content of feature 016 (help-content 1.1.0, DELTA 5, FR-061) on top of the 004 coverage suite: every topic is in
/// the area <c>instruments</c>, reviewed "016", owns its anchors and names its limits.
/// </summary>
public sealed class InstrumentViewsHelpTests
{
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    public static TheoryData<string, string[]> Topics => new()
    {
        { "chords", ["chords", "chord-sheet"] },
        { "chords-advanced", ["chords-vocabulary", "chord-edit"] },
    };

    [Theory]
    [MemberData(nameof(Topics))]
    public void Topic_IsInTheInstrumentsArea_Reviewed016_WithItsAnchors(string id, string[] anchors)
    {
        var topic = Catalog.Topic(id).ShouldNotBeNull(id);

        (topic.Area, topic.Reviewed).ShouldBe(("instruments", "016"));
        foreach (var anchor in anchors)
        {
            Catalog.TopicForAnchor(anchor)!.Id.ShouldBe(id, anchor);
        }
    }

    [Fact]
    public void Area_ComesAfterMixes_BeforeTheGlossary() =>
        TopicParser.AreaOrder.ShouldBe(["start", "import", "tracks", "detail", "mixer", "mixes", "instruments", "glossary"]);

    [Fact]
    public void ChordTopics_NameTheLimits_AndTheBtcCredit()
    {
        Catalog.Topic("chords")!.Markdown.ShouldContain("80 %");
        Catalog.Topic("chords")!.Markdown.ShouldContain("Park et al. 2019, MIT licence");
        Catalog.Topic("chords-advanced")!.Levels.Select(l => l.ToString()).ShouldBe(["Advanced", "Expert"]);
    }
}

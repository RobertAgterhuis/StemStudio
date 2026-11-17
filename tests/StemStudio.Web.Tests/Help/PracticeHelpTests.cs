using StemStudio.Web.Help;

namespace StemStudio.Web.Tests.Help;

/// <summary>Help content of feature 012 (T041, FR-016) on top of the 004 coverage suite (markers, anchors, links, tours).</summary>
public sealed class PracticeHelpTests
{
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    [Theory]
    [InlineData("practice-speed", "mixer", "practice-speed")]
    [InlineData("practice-transpose", "mixer", "practice-transpose")]
    [InlineData("practice-sound-quality", "mixer", "practice-sound-quality")]
    [InlineData("semitone", "glossary", null)]
    public void Topics_Exist_ReviewedFor012_WithTheirAnchors(string id, string area, string? anchor)
    {
        var topic = Catalog.Topic(id).ShouldNotBeNull(id);

        topic.Area.ShouldBe(area);
        topic.Reviewed.ShouldBe("012");
        if (anchor is not null)
        {
            Catalog.TopicForAnchor(anchor)!.Id.ShouldBe(id);
        }
    }

    [Fact]
    public void RelatedLinks_ConnectThePracticeTopics_WithTheMixerAndExportTopics()
    {
        Catalog.Topic("mixer-screen")!.Related.ShouldContain("practice-speed");
        Catalog.Topic("export-dialog")!.Related.ShouldContain("practice-speed");
        Catalog.Topic("practice-speed")!.Related.ShouldContain("practice-sound-quality");
        Catalog.Topic("practice-transpose")!.Related.ShouldContain("semitone");
    }

    [Fact]
    public void SoundQuality_NamesTheLimits_AndLinksTheSoundTouchLicenceAndSource()
    {
        var topic = Catalog.Topic("practice-sound-quality")!;
        var html = Catalog.Render("practice-sound-quality").Html;

        topic.Markdown.ShouldContain("70 %");
        topic.Markdown.ShouldContain("formant");
        topic.Markdown.ShouldContain("meters");
        topic.Markdown.ShouldContain("original speed and key");
        html.ShouldContain("href=\"/third-party/soundtouch/index.html\"");
        html.ShouldContain("LGPL-2.1");
    }

    [Fact]
    public void ThirdPartyLinks_OpenInANewTab_OtherLocalPathsStayText()
    {
        var html = Catalog.Render("practice-sound-quality").Html;

        html.ShouldContain("target=\"_blank\"");
        HelpRenderer.Render(Topic("[x](/api/tracks)")).Html.ShouldNotContain("href=\"/api/tracks\"");
        HelpRenderer.Render(Topic("[x](/third-party/../api)")).Html.ShouldNotContain("href=");
    }

    [Fact]
    public void BeginnerMixerTour_Version2_VisitsTheSpeedControl()
    {
        var tour = Catalog.Tours.Single(t => t.Id == "mixer-basics");

        // The step points at the panel's disclosure button, which is visible also while the panel is closed (a hidden
        // target would be skipped).
        tour.Version.ShouldBe(2);
        tour.Steps.Select(s => (s.Target, s.Topic)).ShouldContain(("practice-panel", "practice-speed"));
        tour.Steps.Count.ShouldBeLessThanOrEqualTo(8);
    }

    [Theory]
    [InlineData("slow down", "practice-speed")]
    [InlineData("transpose", "practice-transpose")]
    [InlineData("semitone", "semitone")]
    [InlineData("smeary", "practice-sound-quality")]
    [InlineData("count-in bars", "practice-count-in")]
    [InlineData("speed trainer", "practice-speed-trainer")]
    [InlineData("sound quality", "practice-sound-quality")]
    [InlineData("count-in", "count-in")]
    public void ReferenceQueries_FindThePracticeTopicsFirst(string query, string expected) =>
        new HelpSearch(Catalog).Search(query)[0].TopicId.ShouldBe(expected);

    private static HelpTopic Topic(string markdown) => new()
    {
        Id = "sample",
        Title = "Sample",
        Area = "mixer",
        Summary = "Sample.",
        Reviewed = "012",
        Markdown = markdown,
    };
}

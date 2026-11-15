using StemStudio.Web.Help;

namespace StemStudio.Web.Tests.Mixer.Karaoke;

/// <summary>Help topics of feature 006 (FR-015; ids from ui-test-ids 1.6.0, no <c>help-</c> prefix).</summary>
public sealed class KaraokeHelpTests
{
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    [Theory]
    [InlineData("karaoke", "mixer", null)]
    [InlineData("karaoke-offset", "mixer", "karaoke.offset")]
    public void BannerTopics_ExplainTheirMarkers(string id, string area, string? feature)
    {
        var topic = Catalog.Topic(id).ShouldNotBeNull();

        Catalog.TopicForAnchor(id)!.Id.ShouldBe(id);
        (topic.Area, topic.Feature, topic.Reviewed).ShouldBe((area, feature, "006"));
    }

    [Fact]
    public void KaraokeTopic_MentionsCopyrightAndTheCountdown()
    {
        var body = Catalog.Topic("karaoke")!.Markdown;

        body.ShouldContain("personal practice");
        body.ShouldContain("countdown");
        body.ShouldContain("help:karaoke-offset");
    }
}

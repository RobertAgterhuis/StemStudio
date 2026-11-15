using StemStudio.Web.Help;

namespace StemStudio.Web.Tests.Artwork;

/// <summary>Help topics of feature 005 (FR-015; ids from ui-test-ids 1.4.0, no <c>help-</c> prefix).</summary>
public sealed class ArtworkHelpTests
{
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    [Fact]
    public void Artwork_ExplainsTheCoverMarker()
    {
        var topic = Catalog.Topic("artwork").ShouldNotBeNull();

        Catalog.TopicForAnchor("artwork")!.Id.ShouldBe("artwork");
        topic.Reviewed.ShouldBe("005");
    }
}

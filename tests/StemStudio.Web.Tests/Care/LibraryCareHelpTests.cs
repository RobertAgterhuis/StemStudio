using StemStudio.Web.Help;

namespace StemStudio.Web.Tests.Care;

/// <summary>Help content of feature 013 (FR-030, help-content 1.0.0) on top of the 004 coverage suite (markers, anchors, links).</summary>
public sealed class LibraryCareHelpTests
{
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    [Theory]
    [InlineData("library-care", "care-page")]
    [InlineData("backup-contents", "backup-dialog")]
    [InlineData("backup-contents", "backup-estimate")]
    [InlineData("backup-folder", "backup-folder-state")]
    [InlineData("backup-folder", "backup-folder-error")]
    [InlineData("backup-compatibility", "backup-row-status-stemstudio-backup-20261117-101500.zip")]
    [InlineData("backup-voice-data", "backup-voice-notice")]
    [InlineData("backup-voice-data", "backup-details-voice-a1b2c3d4-0000-4000-8000-0000000000f1")]
    [InlineData("storage-overview", "care-tab-storage")]
    public void Topics_AreReviewedFor013_InAreaMixes_AndOwnTheirAnchors(string id, string anchor)
    {
        var topic = Catalog.Topic(id).ShouldNotBeNull(id);

        (topic.Area, topic.Reviewed).ShouldBe(("mixes", "013"));
        Catalog.TopicForAnchor(anchor)!.Id.ShouldBe(id);
    }

    [Fact]
    public void LibraryCare_IsTheScreenTopicOfCare()
    {
        Catalog.ScreenTopic("/care")!.Id.ShouldBe("library-care");
    }

    [Theory]
    [InlineData("backup-contents", "not encrypted")]
    [InlineData("backup-contents", "Never included")]
    [InlineData("backup-folder", "STEMSTUDIO_BACKUP_HOST_DIR")]
    [InlineData("backup-folder", "1654")]
    [InlineData("backup-voice-data", "not encrypted")]
    [InlineData("backup-compatibility", "newer StemStudio")]
    [InlineData("library-care", "One job at a time")]
    public void Topics_StateTheLimits(string id, string text) => Catalog.Topic(id)!.Markdown.ShouldContain(text);
}

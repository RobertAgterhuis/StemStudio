using StemStudio.Web.Help;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Help;

/// <summary>The four 022 help topics (022 T084, FR-035; help-content 1.0.0, ui-test-ids 1.22.0 "Help topics").</summary>
public sealed class LibraryHelpTests
{
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    public static TheoryData<string, string> Topics => new()
    {
        { "library-folders", "Library folders" },
        { "library-search", "Library search" },
        { "library-info", "Library info" },
        { "library-art", "Folder pictures" },
    };

    [Theory]
    [MemberData(nameof(Topics))]
    public void Topic_IsInTheLibraryArea_ReviewedFor022_AndAnchoredOnItsMarker(string id, string title)
    {
        var topic = Catalog.Topic(id).ShouldNotBeNull();

        (topic.Title, topic.Area, topic.Reviewed).ShouldBe((title, "mixes", "022"));
        topic.Anchors.ShouldBe([id]);
        Catalog.TopicForAnchor(id)!.Id.ShouldBe(id);
        topic.Levels.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("library-folders", "Components/Library/FolderView.razor")]
    [InlineData("library-art", "Components/Library/FolderView.razor")]
    [InlineData("library-search", "Components/Library/LibrarySearchBox.razor")]
    [InlineData("library-info", "Components/Library/LibraryInfoDialog.razor")]
    public void Marker_IsInTheMarkup(string id, string file) =>
        RepoPaths.ReadWeb(file).ShouldContain($"data-help-id=\"{id}\"");

    [Fact]
    public void FolderTopic_IsTheScreenTopicOfFolderPages_AndTheRootKeepsThe007Topic()
    {
        Catalog.Topics.Single(t => t.Route == "/library/{folderid}").Id.ShouldBe("library-folders");
        Catalog.Topics.Single(t => t.Route == "/library").Id.ShouldBe("library");
    }

    [Theory]
    [InlineData("snare", "library-search")]
    [InlineData("unknown artist", "library-folders")]
    [InlineData("automatic", "library-info")]
    [InlineData("mosaic", "library-art")]
    public void HelpSearch_FindsTheTopics(string query, string expected) =>
        new HelpSearch(Catalog).Search(query)[0].TopicId.ShouldBe(expected);

    [Fact]
    public void Topics_SayPicturesStayInTheApp_AndTheSlashKey()
    {
        Catalog.Topic("library-folders")!.Markdown.ShouldContain("pictures are shown in the app\nonly");
        Catalog.Topic("library-art")!.Markdown.ShouldContain("never part of a download, an export or a backup");
        Catalog.Topic("library-search")!.Markdown.ShouldContain("Press **/**");
    }
}

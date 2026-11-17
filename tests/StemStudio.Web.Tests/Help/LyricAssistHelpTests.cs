using StemStudio.Web.Help;

namespace StemStudio.Web.Tests.Help;

/// <summary>023 T072: the six lyric-assistant help topics (ui-test-ids 1.23.0, FR-030) on top of the 004 coverage suite.</summary>
public sealed class LyricAssistHelpTests
{
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    [Theory]
    [InlineData("lyric-assist")]
    [InlineData("lyric-assist-licence")]
    [InlineData("lyric-assist-devices")]
    [InlineData("lyric-assist-rhythm")]
    [InlineData("lyric-assist-privacy")]
    [InlineData("lyric-assist-limits")]
    public void Topics_Exist_InMixes_ReviewedFor023_AndAreTheirOwnAnchor(string id)
    {
        var topic = Catalog.Topic(id).ShouldNotBeNull(id);

        (topic.Area, topic.Reviewed).ShouldBe(("mixes", "023"));
        Catalog.TopicForAnchor(id)!.Id.ShouldBe(id);
    }

    [Fact]
    public void Editor_HasItsScreenTopic() => Catalog.Topics.Single(t => t.Route == "/lyrics/{id}").Id.ShouldBe("lyric-assist");

    [Theory]
    [InlineData("poetic licence", "lyric-assist-licence")]
    [InlineData("rhythm target", "lyric-assist-rhythm")]
    [InlineData("anthropic", "lyric-assist-privacy")]
    [InlineData("own lyrics", "lyric-assist-limits")]
    [InlineData("internal rhyme", "lyric-assist-devices")]
    [InlineData("lyric assistant", "lyric-assist")]
    public void HelpSearch_FindsTheTopics(string query, string id) =>
        new HelpSearch(Catalog).Search(query, 5).Select(h => h.TopicId).ShouldContain(id);

    [Fact]
    public void Texts_NameTheBindingFacts()
    {
        Catalog.Topic("lyric-assist")!.Markdown.ShouldContain("Ctrl+Alt+C");
        Catalog.Topic("lyric-assist-limits")!.Markdown.ShouldContain("7 or more words");
        Catalog.Topic("lyric-assist-privacy")!.Markdown.ShouldContain("Nothing is sent before you agree");
        Catalog.Topic("lyric-assist-rhythm")!.Markdown.ShouldContain("sends nothing anywhere");
        Catalog.Topic("lyric-assist-licence")!.Markdown.ShouldContain("never reported as errors");
    }
}

using StemStudio.Web.Help;
using static StemStudio.Web.Tests.Help.HelpSamples;

namespace StemStudio.Web.Tests.Help;

public sealed class HelpSearchTests
{
    private static HelpSearch Search(params HelpFile[] files) => new(new HelpCatalog(files));

    private static HelpFile T(string id, string title, TopicText? text = null) =>
        TopicFile(id, content: Topic(id, (text ?? Text()) with { Title = title }));

    private static TopicText Text(string summary = "Nothing.", string keywords = "[]", string body = "Nothing.") =>
        new(null, summary, $"keywords: {keywords}", body);

    [Fact]
    public void Ranks_TitleOverKeywordOverSummaryOverBody()
    {
        var search = Search(
            T("in-body", "Alpha", Text(body: "the loop is here")),
            T("in-summary", "Beta", Text(summary: "About the loop.")),
            T("in-keyword", "Gamma", Text(keywords: "[loop]")),
            T("in-title", "Loop"));

        search.Search("loop").Select(h => h.TopicId).ShouldBe(["in-title", "in-keyword", "in-summary", "in-body"]);
    }

    [Fact]
    public void Scores_FollowTheResearchWeights()
    {
        var search = Search(T("a", "Loop", Text(summary: "A loop.", keywords: "[loop]", body: "loop")));

        search.Search("loop").Single().Score.ShouldBe(5 + 3 + 2 + 1);
    }

    [Fact]
    public void PrefixMatches_Count_ButLessThanWholeWords()
    {
        var search = Search(T("whole", "Metronome"), T("prefix", "Metronomes"));

        var hits = search.Search("metronome");

        hits.Select(h => h.TopicId).ShouldBe(["whole", "prefix"]);
        hits[1].Score.ShouldBeGreaterThan(0);
        hits[1].Score.ShouldBeLessThan(hits[0].Score);
    }

    [Fact]
    public void MultiWordQueries_SumTheirTerms()
    {
        var search = Search(T("detail-stems", "Detail stems"), T("stem", "Stem"), T("track-delete", "Delete a track"), T("tracks", "Tracks"));

        search.Search("detail stems")[0].TopicId.ShouldBe("detail-stems");
        search.Search("delete track")[0].TopicId.ShouldBe("track-delete");
    }

    [Fact]
    public void Matching_IsCaseAndDiacriticInsensitive_AndKeepsHyphenatedTerms()
    {
        var search = Search(T("a", "Café tempo", Text(keywords: "[a-b]")), T("b", "Other"));

        search.Search("CAFE")[0].TopicId.ShouldBe("a");
        search.Search("a-b")[0].TopicId.ShouldBe("a");
    }

    [Fact]
    public void Ties_AreOrderedByTitle()
    {
        var search = Search(T("z", "Zebra loop"), T("y", "Apple loop"));

        search.Search("loop").Select(h => h.TopicId).ShouldBe(["y", "z"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("--")]
    public void BlankQueries_ReturnNothing(string query) => Search(T("a", "Loop")).Search(query).ShouldBeEmpty();

    [Fact]
    public void NoMatch_ReturnsNothing_AndMaxIsRespected()
    {
        var search = Search(T("a", "Loop one"), T("b", "Loop two"), T("c", "Loop three"));

        search.Search("piano").ShouldBeEmpty();
        search.Search("loop", max: 2).Count.ShouldBe(2);
    }
}

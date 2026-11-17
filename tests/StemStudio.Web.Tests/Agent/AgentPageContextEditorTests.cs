using StemStudio.Web.Agent;

namespace StemStudio.Web.Tests.Agent;

/// <summary>Page contexts of the library folders (022 T021/T081) and the lyrics editor (023 T058: <c>PageContext.lyricsId</c>).</summary>
public sealed class AgentPageContextEditorTests
{
    private const string Sheet = "2c4e6a8b-0d1f-4a3b-8c5d-7e9f1a3b5c7d";

    private static StemStudio.Contracts.Agent.PageContextDto From(string path) =>
        AgentPageContexts.From(new Uri($"http://localhost/{path}"), new Uri("http://localhost/"));

    [Theory]
    [InlineData("library/ar0123456789abcdef")]
    [InlineData("library/so0123456789abcdef?view=all")]
    [InlineData("library?q=ace")]
    public void LibraryFolder_IsTheLibraryContext(string path) =>
        (From(path).Page, From(path).LyricsId).ShouldBe(("library", null));

    [Theory]
    [InlineData($"lyrics/{Sheet}")]
    [InlineData($"lyrics/{Sheet}?assist=5a6b7c8d-9e0f-4a1b-8c2d-3e4f5a6b7c8d")]
    public void LyricsEditor_CarriesTheSheet(string path)
    {
        var context = From(path);

        (context.Page, context.LyricsId, context.TrackId).ShouldBe(("lyrics", Guid.Parse(Sheet), null));
    }

    [Theory]
    [InlineData("lyrics")]
    [InlineData("lyrics/not-a-guid")]
    public void LyricsList_HasNoSheet(string path) => (From(path).Page, From(path).LyricsId).ShouldBe(("lyrics", null));
}

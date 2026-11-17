using Bunit;
using StemStudio.Web.Components.Generation;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Generation;

/// <summary>023 T058: the take details show the assisted lines of the lyrics snapshot (FR-024; ui-test-ids take-lyrics-assisted).</summary>
public sealed class TakeDetailsLyricsTests : PageTestContext
{
    private IRenderedComponent<TakeDetails> Render(int assisted)
    {
        var song = SongData.Song();
        song = song with { Song = song.Song! with { Lyrics = song.Song!.Lyrics with { AssistedLineCount = assisted } } };
        return Render<TakeDetails>(p => p.Add(x => x.Generation, song).Add(x => x.Take, song.Takes[0]));
    }

    [Theory]
    [InlineData(3, "Lyrics: 3 lines AI-assisted")]
    [InlineData(1, "Lyrics: 1 line AI-assisted")]
    public void AssistedLines_AreShown(int count, string text) =>
        Render(count).Find("[data-testid=take-lyrics-assisted]").TextContent.ShouldBe(text);

    [Fact]
    public void WithoutAssistedLines_TheRowIsAbsent() =>
        Render(0).FindAll("[data-testid=take-lyrics-assisted]").ShouldBeEmpty();
}

using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Pages.Lyrics;

/// <summary>Search again and remove on the lyrics card (006 T053, FR-009; ui-test-ids 1.6.0).</summary>
public sealed class LyricsCorrectionTests : LyricsTestContext
{
    [Theory]
    [InlineData(LyricsStatusDto.Removed)]
    [InlineData(LyricsStatusDto.NotFound)]
    [InlineData(LyricsStatusDto.Failed)]
    [InlineData(LyricsStatusDto.Disabled)]
    [InlineData(LyricsStatusDto.Found)]
    public async Task SearchAgain_IsOffered_AndStartsANewSearch(LyricsStatusDto status)
    {
        Api.SearchLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(LyricsData.Empty(LyricsStatusDto.Searching));
        var page = RenderSection(status == LyricsStatusDto.Found ? LyricsData.Found() : LyricsData.Empty(status), LyricsData.Empty(LyricsStatusDto.NotFound));

        await page.Find("[data-testid=lyrics-search]").ClickAsync(new());

        await Api.Received(1).SearchLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>());
        page.Find("[data-testid=lyrics-status]").GetAttribute("data-status").ShouldBe("searching");
        Timer.Tick();
        page.WaitForAssertion(() => page.Find("[data-testid=lyrics-status]").GetAttribute("data-status").ShouldBe("notFound"));
    }

    [Theory]
    [InlineData(LyricsStatusDto.Searching)]
    [InlineData(LyricsStatusDto.Instrumental)]
    [InlineData(LyricsStatusDto.AlignPending)]
    [InlineData(LyricsStatusDto.None)]
    public void SearchAgain_IsNotOfferedOtherwise(LyricsStatusDto status) =>
        RenderSection(LyricsData.Empty(status)).FindAll("[data-testid=lyrics-search]").ShouldBeEmpty();

    [Fact]
    public async Task SearchConflict_IsShown()
    {
        Api.SearchLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.Conflict, "The lyrics are being searched or aligned."));
        var page = RenderSection(LyricsData.Empty(LyricsStatusDto.NotFound));

        await page.Find("[data-testid=lyrics-search]").ClickAsync(new());

        page.Find("[data-testid=lyrics-error]").TextContent.ShouldBe("The lyrics are being searched or aligned.");
    }

    [Fact]
    public async Task Remove_AsksFirst_ThenRemovesTheLyrics()
    {
        Confirm.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        Api.RemoveLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(LyricsData.Empty(LyricsStatusDto.Removed));
        var page = RenderSection(LyricsData.Found());

        await page.Find("[data-testid=lyrics-remove]").ClickAsync(new());

        await Confirm.Received(1).ConfirmAsync("Remove lyrics", Arg.Is<string>(m => m.Contains("search for them again", StringComparison.Ordinal)), "Remove");
        page.Find("[data-testid=lyrics-status]").GetAttribute("data-status").ShouldBe("removed");
        page.FindAll("[data-testid=lyrics-remove]").ShouldBeEmpty();
        page.Find("[data-testid=lyrics-search]").ShouldNotBeNull();
    }

    [Fact]
    public async Task Remove_Declined_KeepsTheLyrics()
    {
        Confirm.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var page = RenderSection(LyricsData.Found());

        await page.Find("[data-testid=lyrics-remove]").ClickAsync(new());

        await Api.DidNotReceiveWithAnyArgs().RemoveLyricsAsync(default, Xunit.TestContext.Current.CancellationToken);
        page.Find("[data-testid=lyrics-status]").GetAttribute("data-status").ShouldBe("found");
    }

    [Theory]
    [InlineData(LyricsStatusDto.None)]
    [InlineData(LyricsStatusDto.Removed)]
    [InlineData(LyricsStatusDto.Searching)]
    [InlineData(LyricsStatusDto.Aligning)]
    public void Remove_IsNotOfferedWithoutLyricsOrWhileBusy(LyricsStatusDto status) =>
        RenderSection(LyricsData.Empty(status)).FindAll("[data-testid=lyrics-remove]").ShouldBeEmpty();

    [Fact]
    public void Remove_IsHiddenAtBeginner()
    {
        RenderAt(ExperienceLevel.Beginner, LyricsData.Found()).FindAll("[data-testid=lyrics-remove]").ShouldBeEmpty();
        RenderAt(ExperienceLevel.Beginner, LyricsData.Found()).FindAll("[data-testid=lyrics-search]").Count.ShouldBe(1);
    }
}

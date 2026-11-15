using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Scores;
using StemStudio.Web.Help;
using StemStudio.Web.Scores;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Scores;

/// <summary>Drum notation needs the drum detail stems (008 T063, FR-009, FR-014).</summary>
public sealed class ScoreDrumsTests : ScoreTestContext
{
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    private static ScoreTarget Drums => ScoreTarget.ForChannel(TestData.Track(), "drums");

    [Fact]
    public void BeforeTheSplit_TheDialogAsksToSplitTheDrums()
    {
        var page = RenderDialog(ScoreData.DrumOptions(), Drums);

        var notice = page.Find("[data-testid=score-drums-split-required]");
        notice.GetAttribute("data-help-id").ShouldBe("sheet-music-drums");
        notice.TextContent.ShouldContain("split the drums");
        notice.QuerySelector("[data-testid=score-drums-split-start]").ShouldNotBeNull();
        page.Find("[data-testid=score-submit]").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task SplitStart_StartsTheDetailPhase()
    {
        Api.StartDetailAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track());
        var page = RenderDialog(ScoreData.DrumOptions(), Drums);

        await page.Find("[data-testid=score-drums-split-start]").ClickAsync(new());

        await Api.Received(1).StartDetailAsync(TestData.TrackId, Arg.Any<CancellationToken>());
        page.Find("[data-testid=score-drums-split-required]").TextContent.ShouldContain("The drums are being split");
        page.FindAll("[data-testid=score-drums-split-start]").ShouldBeEmpty();
    }

    [Fact]
    public async Task SplitRefusal_IsShown()
    {
        Api.StartDetailAsync(TestData.TrackId, Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.Conflict, "The detail phase is already queued."));
        var page = RenderDialog(ScoreData.DrumOptions(), Drums);

        await page.Find("[data-testid=score-drums-split-start]").ClickAsync(new());

        page.Find("[data-testid=score-error]").TextContent.ShouldBe("The detail phase is already queued.");
        page.Find("[data-testid=score-drums-split-start]").ShouldNotBeNull();
    }

    [Fact]
    public async Task AfterTheSplit_DrumsCanBeNotated()
    {
        var page = RenderDialog(ScoreData.DrumOptions(requireSplit: false), Drums);
        page.FindAll("[data-testid=score-drums-split-required]").ShouldBeEmpty();

        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        SentRequest().Profile.ShouldBe(ScoreProfileDto.Drums);
        SentRequest().KeyOverride.ShouldBeNull();
    }

    [Fact]
    public void DrumsTopic_ExplainsTheSplit()
    {
        var topic = Catalog.Topic("sheet-music-drums").ShouldNotBeNull();

        Catalog.TopicForAnchor("sheet-music-drums")!.Id.ShouldBe("sheet-music-drums");
        topic.Reviewed.ShouldBe("008");
        topic.Markdown.ShouldContain("Split further");
        topic.Markdown.ShouldContain("hi-hat");
    }
}

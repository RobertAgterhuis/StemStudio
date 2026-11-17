using Bunit;
using NSubstitute;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;
using StemStudio.Web.Components.Library;
using StemStudio.Web.Experience;
using StemStudio.Web.Scores;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Pages;

namespace StemStudio.Web.Tests.Scores;

/// <summary>A part of the song (008 T068, FR-013, FR-014).</summary>
public sealed class ScoreRangeTests : ScoreTestContext
{
    [Fact]
    public async Task Range_IsSentWhenGiven()
    {
        var page = RenderDialog(ScoreData.Options());
        page.Find("[data-testid=score-range-start]").GetAttribute("data-help-id").ShouldBe("sheet-music");

        await page.Find("[data-testid=score-range-start]").InputAsync(new() { Value = "30" });
        await page.Find("[data-testid=score-range-end]").InputAsync(new() { Value = "75.5" });
        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        SentRequest().Range.ShouldBe(new RangeDto(30, 75.5));
    }

    [Theory]
    [InlineData("30", "", "Enter start and end in seconds")]
    [InlineData("30", "31", "at least 2 s long")]
    [InlineData("30", "300", "inside the song (0 to 4:22)")]
    public async Task Range_IsChecked(string start, string end, string message)
    {
        var page = RenderDialog(ScoreData.Options());

        await page.Find("[data-testid=score-range-start]").InputAsync(new() { Value = start });
        await page.Find("[data-testid=score-range-end]").InputAsync(new() { Value = end });
        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        page.Find("[data-testid=score-error]").TextContent.ShouldContain(message);
    }

    [Fact]
    public void Range_IsHiddenAtBeginner() =>
        RenderDialogAt(ExperienceLevel.Beginner, ScoreData.Options()).FindAll("[data-testid=score-range-start]").ShouldBeEmpty();

    [Fact]
    public void LibraryItemTarget_DescribesTheItem()
    {
        var target = ScoreTarget.ForAsset(AssetData.Asset());

        (target.Source, target.SourceKind, target.TrackId).ShouldBe((AudioSourceDto.FromAsset(AssetData.AssetId), "asset", null));
        target.Subject.ShouldBe("the library item \"Never Gonna Give You Up - bass\"");
        target.SuggestedTempo.ShouldBeNull();
    }
}

/// <summary>Sheet music of a library item (008 T068, FR-001, FR-012).</summary>
public sealed class ScoreFromLibraryTests : PageTestContext
{
    private IRenderedComponent<AllItemsView> RenderLibrary(params AssetListDto[] lists)
    {
        Api.ListAssetsAsync(Arg.Any<CancellationToken>()).Returns(lists[0], lists[1..]);
        Api.GetScoreOptionsAsync(Arg.Any<AudioSourceDto>(), Arg.Any<CancellationToken>()).Returns(ScoreData.Options() with
        {
            Source = AudioSourceDto.FromAsset(AssetData.AssetId),
            SuggestedProfile = null,
        });
        Api.CreateScoreAsync(Arg.Any<CreateScoreRequest>(), Arg.Any<CancellationToken>()).Returns(ScoreData.Asset(AssetStatusDto.Queued));
        return Render<AllItemsView>();
    }

    [Fact]
    public void ReadyAudioItems_HaveASheetMusicAction_ScoreAndUnreadyItemsDoNot()
    {
        var page = RenderLibrary(AssetData.List(AssetData.Asset(), ScoreData.Asset(), AssetData.Asset(Guid.NewGuid(), AssetStatusDto.Rendering)));

        page.FindAll("[data-testid^=asset-score-]").Where(e => e.TagName == "BUTTON").Select(b => b.GetAttribute("data-testid"))
            .ShouldBe([$"asset-score-{AssetData.AssetId}"]);
        page.Find($"[data-testid=asset-score-{AssetData.AssetId}]").GetAttribute("aria-label").ShouldBe("Sheet music of Never Gonna Give You Up - bass");
    }

    [Fact]
    public async Task SheetMusicAction_OpensTheDialogForTheItem_AndTheNewItemIsListed()
    {
        var page = RenderLibrary(AssetData.List(AssetData.Asset()), AssetData.List(AssetData.Asset(), ScoreData.Asset(AssetStatusDto.Queued)));

        await page.Find($"[data-testid=asset-score-{AssetData.AssetId}]").ClickAsync(new());
        page.Find("[data-testid=score-dialog]").GetAttribute("data-source").ShouldBe("asset");
        await page.Find("[data-testid=score-profile]").ChangeAsync(new() { Value = "bass" });
        await page.Find("[data-testid=score-submit]").ClickAsync(new());

        await Api.Received(1).GetScoreOptionsAsync(AudioSourceDto.FromAsset(AssetData.AssetId), Arg.Any<CancellationToken>());
        page.WaitForAssertion(() => page.Find($"[data-testid=asset-row-{ScoreData.ScoreId}]").GetAttribute("data-status").ShouldBe("queued"));
        Timer.Waits.ShouldBe(1);
        await page.Find("[data-testid=score-close]").ClickAsync(new());
        page.FindAll("[data-testid=score-dialog]").ShouldBeEmpty();
    }

    [Fact]
    public void SheetMusicAction_IsShownAtBeginner()
    {
        Api.ListAssetsAsync(Arg.Any<CancellationToken>()).Returns(AssetData.List(AssetData.Asset()));

        var page = RenderAtLevel<AllItemsView>(ExperienceLevel.Beginner, _ => { });

        page.FindAll($"[data-testid=asset-score-{AssetData.AssetId}]").Count.ShouldBe(1);
    }
}

/// <summary>Closing the dialog on the track page reloads the track (a drums split may have been queued).</summary>
public sealed class ScoreCloseTests : TrackDetailTestContext
{
    [Fact]
    public async Task ClosingTheDialog_ReloadsTheTrack()
    {
        Api.GetScoreOptionsAsync(Arg.Any<AudioSourceDto>(), Arg.Any<CancellationToken>()).Returns(ScoreData.DrumOptions());
        var page = RenderPage(TestData.Track(), TestData.WithDetail(TestData.Phase(StemStudio.Contracts.Tracks.DetailPhaseStatusDto.Queued)));

        await page.Find("[data-testid=score-stem-drums]").ClickAsync(new());
        await page.Find("[data-testid=score-cancel]").ClickAsync(new());

        page.WaitForAssertion(() => page.Find("[data-testid=detail-status]").TextContent.ShouldNotBeNullOrWhiteSpace());
        await Api.Received(2).GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>());
        Timer.Waits.ShouldBe(1);
    }
}

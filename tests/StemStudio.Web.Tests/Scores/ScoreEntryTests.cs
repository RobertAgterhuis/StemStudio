using Bunit;
using NSubstitute;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;
using StemStudio.Web.Components.Library;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Pages;

namespace StemStudio.Web.Tests.Scores;

/// <summary>Ways into the sheet music dialog on the track page (008 T043; ui-test-ids 1.7.0).</summary>
public sealed class ScoreTrackPageTests : TrackDetailTestContext
{
    public ScoreTrackPageTests() =>
        Api.GetScoreOptionsAsync(Arg.Any<AudioSourceDto>(), Arg.Any<CancellationToken>()).Returns(ScoreData.Options());

    [Fact]
    public void EveryStandardStem_HasASheetMusicButton()
    {
        var page = RenderPage(TestData.Track());

        page.FindAll("[data-testid^=score-stem-]").Select(b => b.GetAttribute("data-testid"))
            .ShouldBe(["score-stem-vocals", "score-stem-drums", "score-stem-bass", "score-stem-guitar", "score-stem-piano", "score-stem-other"]);
        page.Find("[data-testid=score-stem-bass]").GetAttribute("aria-label").ShouldBe("Sheet music of bass");
    }

    [Fact]
    public async Task StemButton_OpensTheDialogForThatChannel()
    {
        var page = RenderPage(TestData.Track());

        await page.Find("[data-testid=score-stem-bass]").ClickAsync(new());

        page.Find("[data-testid=score-dialog]").GetAttribute("data-source").ShouldBe("channel");
        await Api.Received(1).GetScoreOptionsAsync(AudioSourceDto.FromChannel(TestData.TrackId, "bass"), Arg.Any<CancellationToken>());
        await page.Find("[data-testid=score-cancel]").ClickAsync(new());
        page.FindAll("[data-testid=score-dialog]").ShouldBeEmpty();
    }

    [Fact]
    public async Task PitchedDetailStems_HaveSheetMusicButtons_DrumPiecesDoNot()
    {
        var page = RenderPage(TestData.DetailTrack());

        page.FindAll("[data-testid^=score-detail-]").Select(b => b.GetAttribute("data-testid"))
            .ShouldBe(["score-detail-vocals-lead", "score-detail-vocals-backing", "score-detail-vocals-rest"]);
        await page.Find("[data-testid=score-detail-vocals-lead]").ClickAsync(new());
        await Api.Received(1).GetScoreOptionsAsync(AudioSourceDto.FromChannel(TestData.TrackId, "vocals.lead"), Arg.Any<CancellationToken>());
        page.Find("[data-testid=score-dialog]").TextContent.ShouldContain("lead (vocals) of Never Gonna Give You Up");
    }

    [Fact]
    public void DetailButtons_AreHiddenAtBeginner()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.DetailTrack());

        var page = RenderAtLevel<StemStudio.Web.Components.Pages.TrackDetail>(ExperienceLevel.Beginner, p => p.Add(c => c.Id, TestData.TrackId));

        page.FindAll("[data-testid^=score-detail-]").ShouldBeEmpty();
        page.FindAll("[data-testid^=score-stem-]").Count.ShouldBe(6);
    }
}

/// <summary>Score items in the library (008 T043, FR-010; ui-test-ids 1.7.0).</summary>
public sealed class ScoreLibraryTests : PageTestContext
{
    private static readonly string Base = ArtworkData.PublicBase;

    private IRenderedComponent<AllItemsView> RenderLibrary(params AssetDto[] items)
    {
        Api.ListAssetsAsync(Arg.Any<CancellationToken>()).Returns(AssetData.List(items));
        return Render<AllItemsView>();
    }

    [Fact]
    public void ReadyScore_ShowsThePdfPreview_AndTheThreeDownloads()
    {
        var page = RenderLibrary(ScoreData.Asset());
        var id = ScoreData.ScoreId;

        var preview = page.Find($"[data-testid=asset-preview-{id}]");
        preview.TagName.ShouldBe("IFRAME");
        preview.GetAttribute("src").ShouldBe($"{Base}/api/assets/{id}/files/pdf?inline=true");
        preview.GetAttribute("title").ShouldBe("Sheet music preview: Never Gonna Give You Up - bass (sheet music)");
        foreach (var (role, path) in new[] { ("pdf", "pdf"), ("musicxml", "musicxml"), ("midi", "midi") })
        {
            var link = page.Find($"[data-testid=asset-download-{role}-{id}]");
            link.GetAttribute("href").ShouldBe($"{Base}/api/assets/{id}/files/{path}");
            link.HasAttribute("download").ShouldBeTrue();
        }

        page.FindAll($"[data-testid=asset-play-{id}]").ShouldBeEmpty();
        page.FindAll($"[data-testid=asset-download-{id}]").ShouldBeEmpty();
    }

    [Fact]
    public void ReadyScore_ShowsKeyTempoWarningsAndLimits()
    {
        var page = RenderLibrary(ScoreData.Asset());
        var id = ScoreData.ScoreId;

        page.Find($"[data-testid=asset-score-key-{id}]").TextContent.ShouldBe("A-flat major (83 %)");
        page.Find($"[data-testid=asset-score-tempo-{id}]").TextContent.ShouldBe("118 BPM, 4/4");
        page.Find($"[data-testid=asset-score-warnings-{id}]").QuerySelectorAll("li").Select(li => li.TextContent)
            .ShouldBe(["87 very short or out-of-range notes were removed."]);
        page.Find($"[data-testid=asset-score-limits-{id}]").TextContent.ShouldContain("One note at a time");
        page.FindAll($"[data-testid=asset-attribution-{id}]").ShouldBeEmpty();
    }

    [Fact]
    public void ScoreWithoutWarnings_HasNoWarningList_AndDrumsHaveNoKey()
    {
        var facts = ScoreData.Facts(ScoreProfileDto.Drums) with { Warnings = [] };

        var page = RenderLibrary(ScoreData.Asset(facts: facts));

        page.FindAll($"[data-testid=asset-score-warnings-{ScoreData.ScoreId}]").ShouldBeEmpty();
        page.FindAll($"[data-testid=asset-score-key-{ScoreData.ScoreId}]").ShouldBeEmpty();
        page.Find($"[data-testid=asset-score-limits-{ScoreData.ScoreId}]").TextContent.ShouldContain("Kick, snare, toms");
    }

    [Fact]
    public void QueuedScore_HasNoPreviewYet()
    {
        var page = RenderLibrary(ScoreData.Asset(AssetStatusDto.Queued));

        page.FindAll($"[data-testid=asset-preview-{ScoreData.ScoreId}]").ShouldBeEmpty();
        page.Find($"[data-testid=asset-row-{ScoreData.ScoreId}]").GetAttribute("data-status").ShouldBe("queued");
    }
}

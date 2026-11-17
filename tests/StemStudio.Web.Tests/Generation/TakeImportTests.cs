using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Library;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Generation;

/// <summary>Importing a take as a track and the AI labels of generated tracks (009 T065, US4-1/2, FR-008, FR-009).</summary>
public sealed class TakeImportTests : GenerationTestContext
{
    private static readonly Guid Id = GenerationData.GenerationId;

    private static TrackDto GeneratedTrack(Guid? sourceTrackId) => TestData.Track() with
    {
        Id = GenerationData.ImportedTrackId,
        Title = "Never Gonna Give You Up (Jazz trio, take 1)",
        Origin = new TrackOriginDto { Kind = TrackOriginKindDto.Generated, GenerationId = Id, TakeIndex = 1, SourceTrackId = sourceTrackId, AiGenerated = true },
    };

    [Fact]
    public async Task Import_ImportsTheTake_AndOpensTheNewTrack()
    {
        Api.ImportTakeAsync(Id, 1, Arg.Any<CancellationToken>()).Returns(GeneratedTrack(TestData.TrackId));
        var page = RenderList(GenerationData.Generation());

        var button = page.Find($"[data-testid=take-import-{Id}-1]");
        button.TextContent.ShouldContain("Import as track");
        await button.ClickAsync(new());

        await Api.Received(1).ImportTakeAsync(Id, 1, Arg.Any<CancellationToken>());
        NavigatedTo.ShouldEndWith($"/tracks/{GenerationData.ImportedTrackId}");
    }

    [Fact]
    public void AnImportedTake_LinksToItsTrack()
    {
        var generation = GenerationData.Generation() with { Takes = [GenerationData.Take() with { ImportedTrackId = GenerationData.ImportedTrackId }] };

        var page = RenderList(generation);

        var link = page.Find($"[data-testid=take-import-{Id}-1]");
        (link.TagName, link.GetAttribute("href")).ShouldBe(("A", $"tracks/{GenerationData.ImportedTrackId}"));
    }

    [Fact]
    public async Task ImportFailure_IsShown()
    {
        Api.ImportTakeAsync(Id, 1, Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.Conflict, "Generation not completed", "Only takes of a completed generation can be imported."));
        var page = RenderList(GenerationData.Generation());

        await page.Find($"[data-testid=take-import-{Id}-1]").ClickAsync(new());

        page.Find("[data-testid=generation-action-error]").TextContent.ShouldBe("Only takes of a completed generation can be imported.");
        NavigatedTo.ShouldNotContain("/tracks/");
    }

    [Fact]
    public void GeneratedTrackPage_ShowsTheAiBadge_AndLinksToTheSource()
    {
        Api.GetTrackAsync(GenerationData.ImportedTrackId, Arg.Any<CancellationToken>()).Returns(GeneratedTrack(TestData.TrackId));

        var page = Render<TrackDetail>(p => p.Add(c => c.Id, GenerationData.ImportedTrackId));

        var badge = page.Find("[data-testid=track-origin-badge]");
        (badge.TextContent, badge.GetAttribute("data-help-id")).ShouldBe(("AI-generated", "generation-ai-label"));
        var link = page.Find("[data-testid=track-origin-source-link]");
        link.GetAttribute("href").ShouldBe($"tracks/{TestData.TrackId}");
        link.TextContent.ShouldContain("source song");
    }

    [Fact]
    public void GeneratedTrackPage_WithoutTheSource_HasNoLink()
    {
        Api.GetTrackAsync(GenerationData.ImportedTrackId, Arg.Any<CancellationToken>()).Returns(GeneratedTrack(null));

        var page = Render<TrackDetail>(p => p.Add(c => c.Id, GenerationData.ImportedTrackId));

        page.FindAll("[data-testid=track-origin-badge]").Count.ShouldBe(1);
        page.FindAll("[data-testid=track-origin-source-link]").ShouldBeEmpty();
    }

    [Fact]
    public void ImportedTracks_HaveNoAiLabels()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track());
        Api.ListTracksAsync(Arg.Any<CancellationToken>()).Returns([TestData.Track()]);

        Render<TrackDetail>(p => p.Add(c => c.Id, TestData.TrackId)).FindAll("[data-testid=track-origin-badge]").ShouldBeEmpty();
        Render<Tracks>().FindAll($"[data-testid=track-ai-badge-{TestData.TrackId}]").ShouldBeEmpty();
    }

    [Fact]
    public void TrackList_MarksGeneratedTracks_WithAnAiBadge()
    {
        Api.ListTracksAsync(Arg.Any<CancellationToken>()).Returns([GeneratedTrack(TestData.TrackId)]);

        var page = Render<Tracks>();

        page.Find($"[data-testid=track-ai-badge-{GenerationData.ImportedTrackId}]").TextContent.ShouldBe("AI");
    }

    [Fact]
    public void Library_LabelsTakesAsAiGenerated()
    {
        var take = AssetData.Asset() with
        {
            Provenance = AssetData.Asset().Provenance with
            {
                Origin = AssetOriginDto.Generation,
                Generation = new GenerationProvenanceDto { GenerationId = Id, TakeIndex = 1, Kind = GenerationKindDto.Restyle },
            },
        };
        Api.ListAssetsAsync(Arg.Any<CancellationToken>()).Returns(new AssetListDto([take, AssetData.Asset(Guid.NewGuid())], 2, 2048));

        var page = Render<AllItemsView>();

        page.Find($"[data-testid=asset-row-{take.Id}]").TextContent.ShouldContain("AI-generated");
        page.FindAll(".ss-ai-badge").Count.ShouldBe(1);
    }
}

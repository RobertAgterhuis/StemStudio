using System.Net;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;

namespace StemStudio.Api.Tests.Scores;

/// <summary>Feature 008 T066: sheet music of a library audio item (FR-001, FR-012, FR-013; data-model 4.3).</summary>
public sealed class AssetScoreTests : ApiTestBase
{
    [Fact]
    public async Task Options_OfAnAudioItem()
    {
        var (_, bass) = await ReadyBassExportAsync();

        using var response = await Client.GetAsync(new Uri(ApiRoutes.ScoreOptionsForAsset(bass.Id), UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(response, "getScoreOptions");
        var options = await response.BodyAsync<ScoreOptionsDto>();
        (options.Source, options.SuggestedProfile, options.GridStatus, options.DurationSeconds, options.DrumsRequireSplit)
            .ShouldBe((AudioSourceDto.FromAsset(bass.Id), ScoreProfileDto.Bass, GridStatusDto.Reliable, 213.04, false));
        options.AllowedProfiles.Count.ShouldBe(5);
    }

    [Fact]
    public async Task Create_FromAnAudioItemUsesItsFileAndTheShiftedTrackGrid()
    {
        var (track, bass) = await ReadyBassExportAsync();

        using var response = await Client.PostScoreAsync(new CreateScoreRequest { Source = AudioSourceDto.FromAsset(bass.Id), Range = new RangeDto(2, 12) });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(response, "createScore");
        var asset = await response.BodyAsync<AssetDto>();
        (asset.Provenance.SourceAssetIds.Single(), asset.Provenance.TrackId, asset.Provenance.ScoreRecipe!.Profile, asset.Name)
            .ShouldBe((bass.Id, track.Id, ScoreProfileDto.Bass, "Never Gonna Give You Up - bass (sheet music)"));
        var sent = Worker.TranscriptionCreates.Single();
        sent.Input.ShouldBe(new WorkerTranscriptionInput($"assets/{bass.Id:D}/audio.flac"));
        var grid = sent.Grid.ShouldBeOfType<WorkerBeatGridTempo>();
        grid.Beats.ShouldBe(track.BeatGrid!.Beats.Select(beat => Math.Round(beat - 0.5 - 2, 6)).ToList());
    }

    [Fact]
    public async Task AnItemWhoseTrackIsGone_NeedsATempo()
    {
        var (track, bass) = await ReadyBassExportAsync();
        (await Client.DeleteAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct)).Dispose();

        var options = await (await Client.GetAsync(new Uri(ApiRoutes.ScoreOptionsForAsset(bass.Id), UriKind.Relative), Ct)).BodyAsync<ScoreOptionsDto>();
        using var withoutTempo = await Client.PostScoreAsync(new CreateScoreRequest { Source = AudioSourceDto.FromAsset(bass.Id), Profile = ScoreProfileDto.Bass });
        using var withTempo = await Client.PostScoreAsync(new CreateScoreRequest { Source = AudioSourceDto.FromAsset(bass.Id), Profile = ScoreProfileDto.Bass, Tempo = new ScoreTempoDto(120, 4, 0) });

        options.GridStatus.ShouldBe(GridStatusDto.None);
        (await withoutTempo.JsonAsync()).GetProperty("type").GetString().ShouldBe("tempo-required");
        withTempo.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        Worker.TranscriptionCreates.Single().Grid.ShouldBe(new WorkerManualTempo(120, 4, 0));
    }

    [Fact]
    public async Task AMixedItemHasNoSuggestionAndNeedsAProfile()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        var mixed = await ReadyExportAsync(AssetTestData.Raw(track.Id, "bass", "drums"));

        var options = await (await Client.GetAsync(new Uri(ApiRoutes.ScoreOptionsForAsset(mixed.Id), UriKind.Relative), Ct)).BodyAsync<ScoreOptionsDto>();
        using var response = await Client.PostScoreAsync(new CreateScoreRequest { Source = AudioSourceDto.FromAsset(mixed.Id) });

        options.SuggestedProfile.ShouldBeNull();
        (await response.FieldErrorsAsync("profile")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task OnlyReadyAudioItemsAreSources()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        var queued = await (await Client.PostExportAsync(AssetTestData.Raw(track.Id, "bass"))).BodyAsync<AssetDto>();

        using var response = await Client.PostScoreAsync(new CreateScoreRequest { Source = AudioSourceDto.FromAsset(queued.Id), Profile = ScoreProfileDto.Bass });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.FieldErrorsAsync("source.assetId")).ShouldNotBeEmpty();
    }

    private async Task<(Domain.Tracks.Track Track, AssetDto Bass)> ReadyBassExportAsync()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        return (track, await ReadyExportAsync(AssetTestData.Raw(track.Id, "bass") with { Range = new RangeDto(0.5, 200) }));
    }

    private async Task<AssetDto> ReadyExportAsync(CreateAudioExportRequest request)
    {
        var asset = await (await Client.PostExportAsync(request)).BodyAsync<AssetDto>();
        AssetTestData.CompleteRenders(Worker);
        await Factory.SyncAssetsAsync();
        return asset;
    }
}

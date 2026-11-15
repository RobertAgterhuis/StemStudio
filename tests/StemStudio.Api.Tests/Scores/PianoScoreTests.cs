using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;
using static StemStudio.Api.Tests.Scores.ScoreTestData;

namespace StemStudio.Api.Tests.Scores;

/// <summary>Feature 008 T052: piano quality, estimates from <c>StemStudio:ScoreEstimates</c>, attribution and the CPU warning (FR-008).</summary>
public sealed class PianoScoreTests : ApiTestBase
{
    [Fact]
    public async Task Options_ListBothPianoQualitiesWithConfiguredEstimates()
    {
        using var factory = new ConfiguredFactory();
        using var client = factory.CreateClient();
        var track = await factory.SeedAsync(AssetTestData.NamedTrack());
        factory.Worker.OnGetHealth = () => Task.FromResult(Health(ProcessingDeviceKind.Gpu, pianoReady: true));

        var options = await (await client.GetAsync(new Uri(ApiRoutes.ScoreOptionsForChannel(track.Id, "piano"), UriKind.Relative), Ct)).BodyAsync<ScoreOptionsDto>();

        options.Estimates.Where(estimate => estimate.Profile == ScoreProfileDto.Piano).ShouldBe(
            [new ScoreEstimateDto(ScoreProfileDto.Piano, ScoreQualityDto.Accurate, 223), new ScoreEstimateDto(ScoreProfileDto.Piano, ScoreQualityDto.Fast, 12)]);
    }

    [Theory]
    [InlineData(ProcessingDeviceKind.Gpu, ScoreQuality.Accurate)]
    [InlineData(ProcessingDeviceKind.Cpu, ScoreQuality.Fast)]
    public async Task TheDefaultQuality_FollowsTheWorkerDevice(ProcessingDeviceKind device, ScoreQuality quality)
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        Worker.OnGetHealth = () => Task.FromResult(Health(device));

        var asset = await (await Client.PostScoreAsync(Request(track.Id, "piano"))).BodyAsync<AssetDto>();

        asset.Provenance.ScoreRecipe!.Quality.ShouldBe(ScoreMapper(quality));
        Worker.TranscriptionCreates.Single().Quality.ShouldBe(quality);
    }

    [Fact]
    public async Task AnExplicitQuality_IsKept()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());

        await Client.PostScoreAsync(Request(track.Id, "piano") with { Quality = ScoreQualityDto.Accurate });

        Worker.TranscriptionCreates.Single().Quality.ShouldBe(ScoreQuality.Accurate);
    }

    [Fact]
    public async Task APianoScore_CarriesTheAttributionAndTheCpuWarning()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        FixedJobId(Worker);
        var asset = await (await Client.PostScoreAsync(Request(track.Id, "piano") with { Quality = ScoreQualityDto.Accurate })).BodyAsync<AssetDto>();
        var summary = Summary(ScoreProfile.Piano, ScoreEngine.PianoCrnn) with
        {
            Quality = ScoreQuality.Accurate,
            Warnings = [new ScoreWarning(ScoreWarningCode.CpuPiano, "The piano model ran on the CPU.")],
        };
        CompleteTranscriptions(Worker, Result(summary));

        await Factory.SyncAssetsAsync();

        var score = (await (await Client.GetAsync(new Uri(ApiRoutes.Asset(asset.Id), UriKind.Relative), Ct)).BodyAsync<AssetDto>()).Score!;
        (score.Engine, score.Quality, score.Attribution).ShouldBe((ScoreEngineDto.PianoCrnn, ScoreQualityDto.Accurate, ProfileCatalog.PianoAttribution));
        score.Warnings.Single().Code.ShouldBe(ScoreWarningCodeDto.CpuPiano);
    }

    [Fact]
    public async Task TheWorkerAttribution_WinsAndOtherEnginesHaveNone()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        FixedJobId(Worker);
        var piano = await (await Client.PostScoreAsync(Request(track.Id, "piano"))).BodyAsync<AssetDto>();
        CompleteTranscriptions(Worker, Result(Summary(ScoreProfile.Piano, ScoreEngine.PianoCrnn) with { Attribution = "Credit from the worker." }));

        await Factory.SyncAssetsAsync();

        (await (await Client.GetAsync(new Uri(ApiRoutes.Asset(piano.Id), UriKind.Relative), Ct)).BodyAsync<AssetDto>()).Score!.Attribution.ShouldBe("Credit from the worker.");
    }

    private static ScoreQualityDto ScoreMapper(ScoreQuality quality) => Api.Scores.ScoreMapper.Map<ScoreQualityDto>(quality);

    /// <summary>An API whose score estimates are configured: piano CRNN GPU 1 s/s, basic-pitch 0.01 s/s, overhead 10 s.</summary>
    private sealed class ConfiguredFactory : IDisposable
    {
        private readonly ApiFactory _inner = new()
        {
            ConfigureServices = services => services.Configure<StemStudioApiOptions>(options => options.ScoreEstimates = new Api.Scores.ScoreEstimateOptions
            {
                PianoCrnnGpu = 1,
                BasicPitchCpu = 0.01,
                OverheadSeconds = 10,
            }),
        };

        public FakeWorkerClient Worker => _inner.Worker;

        public HttpClient CreateClient() => _inner.CreateClient();

        public Task<Track> SeedAsync(Track track) => _inner.SeedAsync(track);

        public void Dispose() => _inner.Dispose();
    }
}

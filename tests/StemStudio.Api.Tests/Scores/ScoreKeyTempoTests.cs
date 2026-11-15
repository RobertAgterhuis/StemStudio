using System.Net;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;
using static StemStudio.Api.Tests.Scores.ScoreTestData;

namespace StemStudio.Api.Tests.Scores;

/// <summary>Feature 008 T046: key override, resolution and the tempo rules (FR-005, FR-006, FR-007; data-model 4.3).</summary>
public sealed class ScoreKeyTempoTests : ApiTestBase
{
    [Fact]
    public async Task KeyAndResolution_ArePassedThrough()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());

        using var response = await Client.PostScoreAsync(Request(track.Id, "bass") with { KeyOverride = "E minor", Resolution = ScoreResolutionDto.Eighth });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var recipe = (await response.BodyAsync<AssetDto>()).Provenance.ScoreRecipe!;
        (recipe.KeyOverride, recipe.Resolution).ShouldBe(("E minor", ScoreResolutionDto.Eighth));
        var sent = Worker.TranscriptionCreates.Single();
        (sent.KeyOverride, sent.Resolution).ShouldBe(("E minor", ScoreResolution.Eighth));
    }

    [Theory]
    [InlineData("H major")]
    [InlineData("e minor")]
    public async Task AnUnknownKeyIs422(string key)
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());

        using var response = await Client.PostScoreAsync(Request(track.Id, "bass") with { KeyOverride = key });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.FieldErrorsAsync("keyOverride")).ShouldNotBeEmpty();
        Worker.TranscriptionCreates.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnUnreliableGridWithoutTempo_Is422TempoRequired()
    {
        var track = await Factory.SeedAsync(UnreliableTrack());

        using var response = await Client.PostScoreAsync(Request(track.Id, "bass"));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "createScore");
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe("tempo-required");
    }

    [Fact]
    public async Task ATempo_IsSentAsAManualGridAndKeptInTheRecipe()
    {
        var track = await Factory.SeedAsync(UnreliableTrack());

        using var response = await Client.PostScoreAsync(Request(track.Id, "bass") with { Tempo = new ScoreTempoDto(96, 3, 0.4), Range = new RangeDto(30, 75) });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await response.BodyAsync<AssetDto>()).Provenance.ScoreRecipe!.Tempo.ShouldBe(new ScoreTempoDto(96, 3, 0.4));
        var sent = Worker.TranscriptionCreates.Single();
        sent.Grid.ShouldBe(new WorkerManualTempo(96, 3, 0.4));
        sent.Range.ShouldBe(new WorkerRenderRange(30, 75));
    }

    [Fact]
    public async Task ARange_ShiftsTheTrackGrid()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());

        using var response = await Client.PostScoreAsync(Request(track.Id, "bass") with { Range = new RangeDto(0.5, 10) });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var grid = Worker.TranscriptionCreates.Single().Grid.ShouldBeOfType<WorkerBeatGridTempo>();
        grid.Beats.ShouldBe(track.BeatGrid!.Beats.Select(beat => Math.Round(beat - 0.5, 6)).ToList());
    }

    [Theory]
    [InlineData(10, 11)]
    [InlineData(200, 300)]
    public async Task AShortOrOutsideRangeIs422(double start, double end)
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());

        using var response = await Client.PostScoreAsync(Request(track.Id, "bass") with { Range = new RangeDto(start, end) });

        (await response.FieldErrorsAsync("range")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ATempoOutsideItsLimitsIs422()
    {
        var track = await Factory.SeedAsync(UnreliableTrack());

        using var response = await Client.PostScoreAsync(Request(track.Id, "bass") with { Tempo = new ScoreTempoDto(400, 13, 0) });

        (await response.FieldErrorsAsync("tempo.bpm")).ShouldNotBeEmpty();
        (await response.FieldErrorsAsync("tempo.beatsPerBar")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Options_ReportAnUnreliableGrid()
    {
        var track = await Factory.SeedAsync(UnreliableTrack());

        var options = await (await Client.GetAsync(new Uri(ApiRoutes.ScoreOptionsForChannel(track.Id, "bass"), UriKind.Relative), Ct)).BodyAsync<ScoreOptionsDto>();

        options.GridStatus.ShouldBe(GridStatusDto.Unreliable);
    }

    private static Track UnreliableTrack(string videoId = "dQw4w9WgXcQ")
    {
        var track = SyncTestData.UrlTrack(videoId);
        var completion = SyncTestData.Completion();
        track.Complete(completion with { BeatGrid = completion.BeatGrid! with { Reliable = false, Confidence = 0.2 } }, SyncTestData.T0);
        return track;
    }
}

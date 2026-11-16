using System.Net;
using System.Net.Http.Json;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;

namespace StemStudio.Api.Tests.Generations;

/// <summary>009 T070: restyle only a section, in place (FR-014, US5).</summary>
public sealed class InPlaceRestyleTests : ApiTestBase
{
    [Fact]
    public async Task InPlace_StoresTheModeAndSendsTheRangeToRepaint()
    {
        var track = await RestyleEndpointTests.SeedTrackAsync(Factory);

        using var response = await PostAsync(track.Id, 15, 30);

        var generation = await response.BodyAsync<GenerationDto>();
        generation.Restyle!.Range.ShouldBe(new SourceRangeDto { StartSeconds = 15, EndSeconds = 30, Mode = RestyleModeDto.InPlace });
        var sent = Factory.Generator.Created.Single().Params["restyle"]!;
        (sent["mode"]!.GetValue<string>(), sent["rangeStartSeconds"]!.GetValue<double>(), sent["rangeEndSeconds"]!.GetValue<double>()).ShouldBe(("inPlace", 15.0, 30.0));
        await ContractAssert.MatchesAsync(response, "createRestyle");
    }

    [Fact]
    public async Task InPlace_TheTakeHasTheLengthOfTheWholeTrack()
    {
        var track = await RestyleEndpointTests.SeedTrackAsync(Factory);
        var generation = await (await PostAsync(track.Id, 15, 30)).BodyAsync<GenerationDto>();
        var jobId = Factory.Generator.Created.Single().Id;
        Factory.Generator.Complete(jobId, new byte[] { 9, 9 });
        Factory.Generator.Job(jobId)["takes"]![0]!["durationSeconds"] = 213.04;

        await Factory.SyncGenerationsAsync();

        using var stored = await Client.GetAsync(new Uri(ApiRoutes.Generation(generation.Id), UriKind.Relative), Ct);
        var dto = await stored.BodyAsync<GenerationDto>();
        dto.Takes.Single().DurationSeconds.ShouldBe(track.DurationSeconds!.Value);
        await ContractAssert.MatchesAsync(stored, "getGeneration");
    }

    [Fact]
    public async Task InPlace_TracksLongerThan480SecondsAre400()
    {
        var track = SyncTestData.UrlTrack();
        track.Complete(SyncTestData.Completion() with { DurationSeconds = 600 }, SyncTestData.T0);
        await Factory.SeedAsync(track);

        using var response = await PostAsync(track.Id, 15, 30);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync("mode")).ShouldBe(["In-place restyles need a track of at most 480 seconds; restyle a range instead."]);
    }

    [Fact]
    public async Task InPlace_TheRangeMustLieInsideTheTrack()
    {
        var track = await RestyleEndpointTests.SeedTrackAsync(Factory);

        using var response = await PostAsync(track.Id, 200, 230);

        (await response.FieldErrorsAsync("rangeEndSeconds")).ShouldNotBeEmpty();
    }

    private Task<HttpResponseMessage> PostAsync(Guid trackId, double start, double end) => Client.PostAsJsonAsync(
        new Uri(ApiRoutes.Restyles(trackId), UriKind.Relative),
        new RestyleRequest { StyleId = RestyleEndpointTests.JazzTrio, Mode = RestyleModeDto.InPlace, RangeStartSeconds = start, RangeEndSeconds = end },
        Ct);
}

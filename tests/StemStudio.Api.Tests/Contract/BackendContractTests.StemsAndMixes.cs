using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Storage;
using StemStudio.Api.Tests.Mixes;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;

namespace StemStudio.Api.Tests.Contract;

/// <summary>T134: every remaining operation (stems, mixes) and the served OpenAPI document.</summary>
public sealed partial class BackendContractTests
{
    [Fact]
    public async Task GetStem_EveryStatusMatchesContract()
    {
        var track = await _factory.SeedAsync(SyncTestData.CompletedTrack());
        await _factory.Services.GetRequiredService<IStemFileStore>()
            .SaveStemAsync(track.Id, "vocals", (s, ct) => s.WriteAsync(new byte[2048], ct).AsTask(), Ct);
        var path = ApiRoutes.Stem(track.Id, "vocals");

        using var full = await GetAsync(path);
        using var partial = await SendAsync(path, request => request.Headers.Range = new RangeHeaderValue(0, 99));
        using var notModified = await SendAsync(path, request => request.Headers.IfNoneMatch.Add(full.Headers.ETag!));
        using var unsatisfiable = await SendAsync(path, request => request.Headers.Range = new RangeHeaderValue(5000, 6000));
        using var notFound = await GetAsync(ApiRoutes.Stem(track.Id, "piano"));

        foreach (var response in new[] { full, partial, notModified, unsatisfiable, notFound })
        {
            await ContractAssert.MatchesAsync(response, "getStem");
        }
    }

    [Fact]
    public async Task ListMixes_200And400MatchContract()
    {
        var track = await _factory.SeedAsync(SyncTestData.CompletedTrack());
        (await CreateMixAsync(track.Id, MixEndpointsTests.Settings())).Dispose();

        using var all = await GetAsync(ApiRoutes.Mixes);
        using var filtered = await GetAsync($"{ApiRoutes.Mixes}?trackId={track.Id}");
        using var invalid = await GetAsync($"{ApiRoutes.Mixes}?trackId=x");

        foreach (var response in new[] { all, filtered, invalid })
        {
            await ContractAssert.MatchesAsync(response, "listMixes");
        }
    }

    [Fact]
    public async Task CreateMix_EveryStatusMatchesContract()
    {
        var track = await _factory.SeedAsync(SyncTestData.CompletedTrack());

        using var created = await CreateMixAsync(track.Id, MixEndpointsTests.Settings());
        using var conflict = await CreateMixAsync(track.Id, MixEndpointsTests.Settings());
        using var invalid = await CreateMixAsync(track.Id, MixEndpointsTests.Settings() with { MasterVolume = 2 });
        using var notFound = await CreateMixAsync(Guid.NewGuid(), MixEndpointsTests.Settings());

        foreach (var response in new[] { created, conflict, invalid, notFound })
        {
            await ContractAssert.MatchesAsync(response, "createMix");
        }
    }

    [Fact]
    public async Task GetUpdateDeleteDuplicateMix_EveryStatusMatchesContract()
    {
        var track = await _factory.SeedAsync(SyncTestData.CompletedTrack());
        var mix = await (await CreateMixAsync(track.Id, MixEndpointsTests.Settings())).BodyAsync<MixDto>();
        (await CreateMixAsync(track.Id, MixEndpointsTests.Settings("Other"))).Dispose();
        var unknown = ApiRoutes.Mix(Guid.NewGuid());

        await ContractAssert.MatchesAsync(await GetAsync(ApiRoutes.Mix(mix.Id)), "getMix");
        await ContractAssert.MatchesAsync(await GetAsync(unknown), "getMix");
        await ContractAssert.MatchesAsync(await PutAsync(ApiRoutes.Mix(mix.Id), MixEndpointsTests.Settings("Renamed")), "updateMix");
        await ContractAssert.MatchesAsync(await PutAsync(ApiRoutes.Mix(mix.Id), MixEndpointsTests.Settings("Other")), "updateMix");
        await ContractAssert.MatchesAsync(await PutAsync(ApiRoutes.Mix(mix.Id), MixEndpointsTests.Settings() with { Loop = new LoopRegionDto(5, 1, true) }), "updateMix");
        await ContractAssert.MatchesAsync(await PutAsync(unknown, MixEndpointsTests.Settings()), "updateMix");
        await ContractAssert.MatchesAsync(await PostAsync(ApiRoutes.DuplicateMix(mix.Id)), "duplicateMix");
        await ContractAssert.MatchesAsync(await PostAsync(ApiRoutes.DuplicateMix(Guid.NewGuid())), "duplicateMix");
        await ContractAssert.MatchesAsync(await _client.DeleteAsync(new Uri(ApiRoutes.Mix(mix.Id), UriKind.Relative), Ct), "deleteMix");
        await ContractAssert.MatchesAsync(await _client.DeleteAsync(new Uri(unknown, UriKind.Relative), Ct), "deleteMix");
    }

    [Fact]
    public async Task DuplicateMix_ConcurrentNameClash409MatchesContract()
    {
        var (mix, mixes) = MixRepositoryStubs.ConcurrentNameClash();
        using var factory = new ApiFactory { ConfigureServices = services => services.AddScoped(_ => mixes) };
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(new Uri(ApiRoutes.DuplicateMix(mix.Id), UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(response, "duplicateMix");
    }

    [Fact]
    public async Task ServedOpenApiDocument_ListsExactlyTheContractOperations()
    {
        using var response = await GetAsync("/openapi/v1.json");
        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync(Ct))!;

        var served = document["paths"]!.AsObject()
            .SelectMany(path => path.Value!.AsObject().Select(operation => operation.Value!["operationId"]?.GetValue<string>()))
            .ToList();
        var contract = OpenApiContract.Backend.Operations.Select(op => op.Operation["operationId"]!.GetValue<string>())
            .Except(PendingArtworkOperations).Except(PendingAssetOperations).Except(PendingLyricsOperations).Except(PendingScoreOperations)
            .Except(PendingGenerationOperations).Except(PendingSongVoiceOperations).ToList();

        served.ShouldBe(contract, ignoreOrder: true);
    }

    // Contract operations not served yet (staged by the architect in the 005/007 Phase 0).
    // Feature 005 (backend 1.4.0): the backend removes each id when it serves it (T026, T039, T046).
    private static readonly string[] PendingArtworkOperations = [];

    // Feature 007 (backend 1.5.0): the backend removes each id per story (T035, T042, T049, T054, T059).
    private static readonly string[] PendingAssetOperations = [];

    // Feature 006 (backend 1.6.0, staged in the 006/008 Phase 0): the backend removes each id per story (T033, T044, T049).
    private static readonly string[] PendingLyricsOperations = [];

    // Feature 008 (backend 1.7.0, staged in the 006/008 Phase 0): the backend removes both ids in T041.
    private static readonly string[] PendingScoreOperations = [];

    // Feature 009 (backend 1.8.0, staged in the 009/010 Phase 0): the backend removes each id per story (T042, T047, T054, T059, T064).
    private static readonly string[] PendingGenerationOperations =
    [
        "getGenerationStatus", "createStyle", "updateStyle", "deleteStyle", "duplicateStyle",
        "importTake",
    ];

    // Feature 010 (backend 1.9.0, staged in the 009/010 Phase 0): the backend removes each id per story (T028, T030, T035, T045, T047, T049).
    private static readonly string[] PendingSongVoiceOperations =
    [
        "listLyricsSheets", "createLyricsSheet", "uploadLyricsSheet", "proposeSections", "getLyricsSheet", "updateLyricsSheet", "deleteLyricsSheet",
        "createSong", "listVoiceProfiles", "createVoiceProfile", "getVoiceProfile", "deleteVoiceProfile", "getVoiceSample",
        "getVoiceConsentStatement", "createVoiceConversion", "getTakeVocal",
    ];

    private Task<HttpResponseMessage> CreateMixAsync(Guid trackId, MixSettingsDto settings) =>
        _client.PostAsJsonAsync(new Uri(ApiRoutes.Mixes, UriKind.Relative), new CreateMixRequest(trackId, settings), Ct);

    private Task<HttpResponseMessage> PutAsync(string path, MixSettingsDto settings) =>
        _client.PutAsJsonAsync(new Uri(path, UriKind.Relative), settings, Ct);

    private async Task<HttpResponseMessage> SendAsync(string path, Action<HttpRequestMessage> configure)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        configure(request);
        return await _client.SendAsync(request, Ct);
    }
}

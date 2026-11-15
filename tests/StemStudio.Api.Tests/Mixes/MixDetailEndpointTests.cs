using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;

namespace StemStudio.Api.Tests.Mixes;

/// <summary>Mixes with expansion and detail channel settings over HTTP (T077; FR-015, SC-005).</summary>
public sealed class MixDetailEndpointTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public MixDetailEndpointTests() => _client = _factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task CreateGetUpdateDuplicate_RoundTripExpansionAndDetailChannels()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailCompleted());

        using var created = await CreateAsync(track.Id, WithDetail());
        var mix = await created.BodyAsync<MixDto>();
        var fetched = await (await GetAsync(ApiRoutes.Mix(mix.Id))).BodyAsync<MixDto>();
        var updated = await (await _client.PutAsJsonAsync(new Uri(ApiRoutes.Mix(mix.Id), UriKind.Relative), WithDetail("Renamed") with { Expanded = ["vocals"] }, Ct)).BodyAsync<MixDto>();
        var copy = await (await _client.PostAsync(new Uri(ApiRoutes.DuplicateMix(mix.Id), UriKind.Relative), null, Ct)).BodyAsync<MixDto>();

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        AssertDetail(mix.Settings, ["drums"]);
        AssertDetail(fetched.Settings, ["drums"]);
        AssertDetail(updated.Settings, ["vocals"]);
        AssertDetail(copy.Settings, ["vocals"]);
        await ContractAssert.MatchesAsync(await GetAsync(ApiRoutes.Mix(mix.Id)), "getMix");
    }

    [Theory]
    [InlineData("""{"parent":"drums","stem":"toms","volume":0.8,"pan":0,"mute":true,"solo":false}""", "settings.detailChannels[0].stem")]
    [InlineData("""{"parent":"guitar","stem":"strings","volume":0.8,"pan":0,"mute":true,"solo":false}""", "settings.detailChannels[0].parent")]
    [InlineData("""{"parent":"drums","stem":"snare","volume":2,"pan":0,"mute":true,"solo":false}""", "settings.detailChannels[0].volume")]
    public async Task Create_400WithJsonPathsForInvalidDetailChannels(string channel, string field)
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailCompleted());

        using var response = await PostRawAsync(track.Id, $"\"expanded\":[],\"detailChannels\":[{channel}]");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync(field)).ShouldNotBeEmpty();
        await ContractAssert.MatchesAsync(response, "createMix");
    }

    [Theory]
    [InlineData("piano")]
    [InlineData("bass")]
    public async Task Create_400ForAnExpandedParentThatIsNotSplit(string parent)
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailCompleted());

        using var response = await CreateAsync(track.Id, WithDetail() with { Expanded = [parent] });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync("settings.expanded[0]")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Update_400UsesPathsWithoutThePrefix()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailCompleted());
        var mix = await (await CreateAsync(track.Id, WithDetail())).BodyAsync<MixDto>();

        using var response = await _client.PutAsJsonAsync(new Uri(ApiRoutes.Mix(mix.Id), UriKind.Relative), WithDetail() with { Expanded = ["piano"] }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync("expanded[0]")).ShouldNotBeEmpty();
        await ContractAssert.MatchesAsync(response, "updateMix");
    }

    [Fact]
    public async Task Create_400ForNullDetailEntries()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailCompleted());

        using var response = await PostRawAsync(track.Id, "\"expanded\":[null],\"detailChannels\":[null]");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync("settings.expanded[0]")).ShouldNotBeEmpty();
        (await response.FieldErrorsAsync("settings.detailChannels[0]")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Create_A110ShapedBodyWithoutTheFieldsIsReturnedWithEmptyArrays()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailCompleted());

        using var response = await PostRawAsync(track.Id, null);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var settings = (await response.JsonAsync()).GetProperty("settings");
        settings.GetProperty("expanded").GetArrayLength().ShouldBe(0);
        settings.GetProperty("detailChannels").GetArrayLength().ShouldBe(0);
        await ContractAssert.MatchesAsync(await GetAsync(ApiRoutes.Mixes + $"?trackId={track.Id}"), "listMixes");
    }

    [Fact]
    public async Task Create_ExplicitNullDetailFieldsAreTreatedAsEmpty()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailCompleted());

        using var response = await PostRawAsync(track.Id, "\"expanded\":null,\"detailChannels\":null");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await response.BodyAsync<MixDto>()).Settings.Expanded.ShouldBeEmpty();
    }

    private static void AssertDetail(MixSettingsDto settings, string[] expanded)
    {
        settings.Expanded.ShouldBe(expanded);
        settings.DetailChannels.ShouldBe([Snare, Lead]);
    }

    private static readonly DetailChannelSettingsDto Snare = new(StemNames.Drums, "snare", 0.8, 0, true, false);

    private static readonly DetailChannelSettingsDto Lead = new(StemNames.Vocals, "lead", 0.5, -0.3, false, true);

    private static MixSettingsDto WithDetail(string name = "No snare") =>
        MixEndpointsTests.Settings(name) with { Expanded = [StemNames.Drums], DetailChannels = [Snare, Lead] };

    private Task<HttpResponseMessage> CreateAsync(Guid trackId, MixSettingsDto settings) =>
        _client.PostAsJsonAsync(new Uri(ApiRoutes.Mixes, UriKind.Relative), new CreateMixRequest(trackId, settings), Ct);

    private Task<HttpResponseMessage> GetAsync(string path) => _client.GetAsync(new Uri(path, UriKind.Relative), Ct);

    /// <summary>Posts a 1.1.0-shaped body, optionally with extra settings members.</summary>
    private async Task<HttpResponseMessage> PostRawAsync(Guid trackId, string? extraSettings)
    {
        var settings = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(MixEndpointsTests.Settings(), ApiFactory.Json))!.AsObject();
        settings.Remove("expanded");
        settings.Remove("detailChannels");
        var settingsJson = settings.ToJsonString();
        if (extraSettings is not null)
        {
            settingsJson = settingsJson[..^1] + "," + extraSettings + "}";
        }

        using var content = new StringContent($"{{\"trackId\":\"{trackId}\",\"settings\":{settingsJson}}}", Encoding.UTF8, "application/json");
        return await _client.PostAsync(new Uri(ApiRoutes.Mixes, UriKind.Relative), content, Ct);
    }
}

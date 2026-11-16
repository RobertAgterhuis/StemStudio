using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Mixes;
using StemStudio.Persistence;

namespace StemStudio.Api.Tests.Mixes;

/// <summary>
/// Feature 012 T035: <c>practice</c> on the mix endpoints (backend-api 1.11.0; FR-014, FR-015, SC-007): round trip, absent =
/// defaults, pre-012 mixes return defaults, validation paths, duplicate, summaries and exports unchanged.
/// </summary>
public sealed class MixPracticeEndpointTests : ApiTestBase
{
    private static readonly PracticeSettingsDto SlowPractice =
        new(75, -2, new SpeedTrainerSettingsDto(true, 60, 5, 100, 2), new CountInSettingsDto(1, true));

    private static MixSettingsDto WithPractice(PracticeSettingsDto practice) => MixEndpointsTests.Settings() with { Practice = practice };

    [Fact]
    public async Task Create_WithPractice_ReturnsItInTheResponseAndOnGet()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        using var created = await CreateAsync(track.Id, WithPractice(SlowPractice));
        var mix = await created.BodyAsync<MixDto>();
        using var get = await GetAsync(mix.Id);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        await ContractAssert.MatchesAsync(created, "createMix");
        await ContractAssert.MatchesAsync(get, "getMix");
        mix.Settings.Practice.ShouldBe(SlowPractice);
        (await get.BodyAsync<MixDto>()).Settings.Practice.ShouldBe(SlowPractice);
    }

    [Fact]
    public async Task Update_WithPractice_ReplacesIt()
    {
        var mix = await SeedMixAsync(MixEndpointsTests.Settings());

        using var updated = await PutAsync(mix.Id, WithPractice(SlowPractice));

        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(updated, "updateMix");
        (await updated.BodyAsync<MixDto>()).Settings.Practice.ShouldBe(SlowPractice);
        (await GetMixAsync(mix.Id)).Settings.Practice.ShouldBe(SlowPractice);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_WithoutPractice_StoresAndReturnsTheDefaults(bool explicitNull)
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        var body = BodyWithoutPractice(new CreateMixRequest(track.Id, MixEndpointsTests.Settings()), "settings", explicitNull);

        using var response = await Client.PostAsync(new Uri(ApiRoutes.Mixes, UriKind.Relative), body, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var json = await response.JsonAsync();
        json.GetProperty("settings").GetProperty("practice").GetProperty("speedPercent").GetInt32().ShouldBe(100);
        (await response.BodyAsync<MixDto>()).Settings.Practice.ShouldBe(PracticeSettingsDto.Default);
    }

    [Fact]
    public async Task Update_WithoutPractice_ResetsToTheDefaults()
    {
        var mix = await SeedMixAsync(WithPractice(SlowPractice));

        using var response = await Client.PutAsync(new Uri(ApiRoutes.Mix(mix.Id), UriKind.Relative), BodyWithoutPractice(MixEndpointsTests.Settings(), null, false), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.BodyAsync<MixDto>()).Settings.Practice.ShouldBe(PracticeSettingsDto.Default);
    }

    [Fact]
    public async Task Get_AMixSavedBefore012_ReturnsTheDefaults()
    {
        var mix = await SeedMixAsync(WithPractice(SlowPractice));
        await Factory.WithScopeAsync(sp => sp.GetRequiredService<StemStudioDbContext>().Database.ExecuteSqlRawAsync("UPDATE Mixes SET Practice = NULL", Ct));

        using var response = await GetAsync(mix.Id);

        await ContractAssert.MatchesAsync(response, "getMix");
        (await response.BodyAsync<MixDto>()).Settings.Practice.ShouldBe(PracticeSettingsDto.Default);
    }

    public static TheoryData<PracticeSettingsDto, string, string> InvalidPractice => new()
    {
        { PracticeSettingsDto.Default with { SpeedPercent = 49 }, "practice.speedPercent", "Must be between 50 and 120." },
        { PracticeSettingsDto.Default with { SpeedPercent = 121 }, "practice.speedPercent", "Must be between 50 and 120." },
        { PracticeSettingsDto.Default with { TransposeSemitones = 13 }, "practice.transposeSemitones", "Must be between -12 and 12." },
        { WithTrainer(t => t with { StartPercent = 120 }), "practice.trainer.startPercent", "Must be between 50 and 119." },
        { WithTrainer(t => t with { StepPercent = 21 }), "practice.trainer.stepPercent", "Must be between 1 and 20." },
        { WithTrainer(t => t with { TargetPercent = 121 }), "practice.trainer.targetPercent", "Must be between 51 and 120." },
        { WithTrainer(t => t with { PassesPerStep = 0 }), "practice.trainer.passesPerStep", "Must be between 1 and 8." },
        { WithTrainer(t => t with { StartPercent = 90, TargetPercent = 90 }), "practice.trainer.targetPercent", PracticeLimits.TargetAboveStartMessage },
        { PracticeSettingsDto.Default with { CountIn = new CountInSettingsDto(3, false) }, "practice.countIn.bars", "Must be between 0 and 2." },
    };

    [Theory]
    [MemberData(nameof(InvalidPractice))]
    public async Task Update_InvalidPractice_Returns400AtThePracticePath(PracticeSettingsDto practice, string field, string message)
    {
        var mix = await SeedMixAsync(MixEndpointsTests.Settings());

        using var response = await PutAsync(mix.Id, WithPractice(practice));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ContractAssert.MatchesAsync(response, "updateMix");
        (await response.FieldErrorsAsync(field)).ShouldBe([message]);
        (await GetMixAsync(mix.Id)).Settings.Practice.ShouldBe(PracticeSettingsDto.Default);
    }

    [Fact]
    public async Task Create_InvalidPractice_ReportsThePathUnderSettings()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        using var response = await CreateAsync(track.Id, WithPractice(WithTrainer(t => t with { StartPercent = 100, TargetPercent = 80 })));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync("settings.practice.trainer.targetPercent")).ShouldBe([PracticeLimits.TargetAboveStartMessage]);
    }

    [Theory]
    [InlineData("trainer")]
    [InlineData("countIn")]
    public async Task Update_PracticeWithoutAGroup_Returns400Required(string group)
    {
        var mix = await SeedMixAsync(MixEndpointsTests.Settings());
        var node = JsonSerializer.SerializeToNode(WithPractice(SlowPractice), ApiFactory.Json)!;
        node["practice"]!.AsObject().Remove(group);

        using var response = await Client.PutAsync(new Uri(ApiRoutes.Mix(mix.Id), UriKind.Relative), Json(node.ToJsonString()), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync($"practice.{group}")).ShouldBe([MixValidatorRequired]);
    }

    [Fact]
    public async Task Update_AFractionalPercent_Returns400()
    {
        var mix = await SeedMixAsync(MixEndpointsTests.Settings());
        var json = JsonSerializer.Serialize(WithPractice(SlowPractice), ApiFactory.Json).Replace("\"speedPercent\":75", "\"speedPercent\":75.5", StringComparison.Ordinal);

        using var response = await Client.PutAsync(new Uri(ApiRoutes.Mix(mix.Id), UriKind.Relative), Json(json), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await GetMixAsync(mix.Id)).Settings.Practice.ShouldBe(PracticeSettingsDto.Default);
    }

    [Fact]
    public async Task Update_TrainerAndEveryPassWithoutALoop_AreAccepted()
    {
        var mix = await SeedMixAsync(MixEndpointsTests.Settings() with { Loop = null });

        using var response = await PutAsync(mix.Id, WithPractice(SlowPractice) with { Loop = null });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.BodyAsync<MixDto>()).Settings.Practice.ShouldBe(SlowPractice);
    }

    [Fact]
    public async Task Duplicate_KeepsThePractice()
    {
        var mix = await SeedMixAsync(WithPractice(SlowPractice));

        using var response = await Client.PostAsync(new Uri(ApiRoutes.DuplicateMix(mix.Id), UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        await ContractAssert.MatchesAsync(response, "duplicateMix");
        (await response.BodyAsync<MixDto>()).Settings.Practice.ShouldBe(SlowPractice);
    }

    [Fact]
    public async Task ListMixes_SummariesCarryNoPractice()
    {
        var mix = await SeedMixAsync(WithPractice(SlowPractice));

        using var response = await Client.GetAsync(new Uri($"{ApiRoutes.Mixes}?trackId={mix.TrackId:D}", UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(response, "listMixes");
        var summary = (await response.JsonAsync()).EnumerateArray().Single();
        summary.EnumerateObject().Select(p => p.Name).ShouldBe(["id", "trackId", "trackTitle", "name", "updatedAt"], ignoreOrder: true);
    }

    [Fact]
    public async Task ExportOfASavedMix_IsIdenticalWithAndWithoutPractice()
    {
        var mix = await SeedMixAsync(MixEndpointsTests.Settings());
        var neutral = await ExportAsync(mix);
        (await PutAsync(mix.Id, WithPractice(SlowPractice))).Dispose();

        var practised = await ExportAsync(mix);

        Worker.RenderCreates.Count.ShouldBe(2);
        JsonSerializer.Serialize(Worker.RenderCreates[1]).ShouldBe(JsonSerializer.Serialize(Worker.RenderCreates[0]));
        JsonSerializer.Serialize(practised.Provenance.Recipe).ShouldBe(JsonSerializer.Serialize(neutral.Provenance.Recipe));
        JsonSerializer.Serialize(practised.Provenance.Recipe).ShouldNotContain("ractice");
    }

    private const string MixValidatorRequired = "This value is required.";

    private static PracticeSettingsDto WithTrainer(Func<SpeedTrainerSettingsDto, SpeedTrainerSettingsDto> change) =>
        PracticeSettingsDto.Default with { Trainer = change(SpeedTrainerSettingsDto.Default) };

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    /// <summary>The body as JSON without <c>practice</c> (or with <c>"practice": null</c>) at <paramref name="settingsPath"/>.</summary>
    private static StringContent BodyWithoutPractice<T>(T body, string? settingsPath, bool explicitNull)
    {
        var node = JsonSerializer.SerializeToNode(body, ApiFactory.Json)!;
        var settings = (settingsPath is null ? node : node[settingsPath]!).AsObject();
        settings.Remove("practice");
        if (explicitNull)
        {
            settings["practice"] = null;
        }

        return Json(node.ToJsonString());
    }

    private async Task<AssetDto> ExportAsync(MixDto mix)
    {
        var request = new CreateAudioExportRequest { TrackId = mix.TrackId, Mode = ExportModeDto.Mix, MixId = mix.Id, Format = new ExportFormatDto(AudioFileFormatDto.Flac) };
        using var response = await Client.PostAsJsonAsync(new Uri(ApiRoutes.AudioExports, UriKind.Relative), request, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        return await response.BodyAsync<AssetDto>();
    }

    private async Task<MixDto> SeedMixAsync(MixSettingsDto settings)
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        using var response = await CreateAsync(track.Id, settings);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.BodyAsync<MixDto>();
    }

    private Task<HttpResponseMessage> CreateAsync(Guid trackId, MixSettingsDto settings) =>
        Client.PostAsJsonAsync(new Uri(ApiRoutes.Mixes, UriKind.Relative), new CreateMixRequest(trackId, settings), Ct);

    private Task<HttpResponseMessage> PutAsync(Guid mixId, MixSettingsDto settings) =>
        Client.PutAsJsonAsync(new Uri(ApiRoutes.Mix(mixId), UriKind.Relative), settings, Ct);

    private Task<HttpResponseMessage> GetAsync(Guid mixId) => Client.GetAsync(new Uri(ApiRoutes.Mix(mixId), UriKind.Relative), Ct);

    private async Task<MixDto> GetMixAsync(Guid mixId)
    {
        using var response = await GetAsync(mixId);
        return await response.BodyAsync<MixDto>();
    }
}

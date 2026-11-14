using System.Net;
using System.Net.Http.Json;
using System.Text;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Mixes;

public sealed class MixEndpointsTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public MixEndpointsTests() => _client = _factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    internal static MixSettingsDto Settings(string name = "Guitar practice") => new(
        name,
        0.9,
        [new ChannelSettingsDto(StemNames.Guitar, 0, 0, true, false), new ChannelSettingsDto(StemNames.Drums, 0.9, -0.2, false, true)],
        new MetronomeSettingsDto(true, 0.6, 120.5, -20, 3),
        new LoopRegionDto(10, 20, true));

    [Fact]
    public async Task Create_Returns201WithLocationAndAllSettings()
    {
        var track = await _factory.SeedAsync(SyncTestData.CompletedTrack());

        using var response = await CreateAsync(track.Id, Settings("  Guitar practice  "));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var mix = await response.BodyAsync<MixDto>();
        response.Headers.Location!.ToString().ShouldBe(ApiRoutes.Mix(mix.Id));
        mix.TrackId.ShouldBe(track.Id);
        mix.Settings.Name.ShouldBe("Guitar practice");
        mix.Settings.Channels.ShouldBe(Settings().Channels);
        mix.Settings.Metronome.ShouldBe(Settings().Metronome);
        mix.Settings.Loop.ShouldBe(Settings().Loop);
        mix.Settings.MasterVolume.ShouldBe(0.9);
        mix.CreatedAt.ShouldBe(ApiFactory.Start);
        mix.UpdatedAt.ShouldBe(ApiFactory.Start);
    }

    [Fact]
    public async Task Create_InvalidValuesReturn400WithJsonPathKeys()
    {
        var track = await _factory.SeedAsync(SyncTestData.CompletedTrack());
        var settings = Settings(" ") with
        {
            Metronome = new MetronomeSettingsDto(true, 0.7, 500, 0, 4),
            Loop = new LoopRegionDto(10, 999, true),
        };

        using var response = await CreateAsync(track.Id, settings);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.JsonAsync()).GetProperty("errors");
        errors.TryGetProperty("settings.name", out _).ShouldBeTrue();
        errors.TryGetProperty("settings.metronome.bpmOverride", out _).ShouldBeTrue();
        errors.TryGetProperty("settings.loop.end", out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData("flute")]
    [InlineData("guitar")]
    public async Task Create_StemNotOnTrackReturns400(string stem)
    {
        var track = SyncTestData.UrlTrack();
        track.Complete(SyncTestData.Completion() with { Model = "htdemucs", Stems = StemCatalog.FourStem.Select(n => new Stem(n, 1)).ToList() }, SyncTestData.T0);
        await _factory.SeedAsync(track);

        using var response = await CreateAsync(track.Id, Settings() with { Channels = [new ChannelSettingsDto(stem, 0.5, 0, false, false)] });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync("settings.channels[0].stem")).Single().ShouldContain(stem);
    }

    [Fact]
    public async Task Create_TrackNotCompletedReturns400OnTrackId()
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());

        using var response = await CreateAsync(track.Id, Settings());

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync("trackId")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Create_UnknownTrackReturns404()
    {
        using var response = await CreateAsync(Guid.NewGuid(), Settings());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("Guitar practice")]
    [InlineData("  GUITAR PRACTICE ")]
    public async Task Create_DuplicateNameReturns409(string name)
    {
        var track = await _factory.SeedAsync(SyncTestData.CompletedTrack());
        (await CreateAsync(track.Id, Settings())).Dispose();

        using var response = await CreateAsync(track.Id, Settings(name));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.JsonAsync();
        problem.GetProperty("title").GetString().ShouldBe("Mix name taken");
        problem.GetProperty("detail").GetString().ShouldBe("A mix named \"Guitar practice\" already exists for this track.");
    }

    [Theory]
    [InlineData("{}", "settings")]
    [InlineData("{\"trackId\":\"0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a\",\"settings\":{\"name\":\"x\",\"masterVolume\":1,\"channels\":[]}}", "settings.metronome")]
    [InlineData("{\"trackId\":\"0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a\",\"settings\":{\"masterVolume\":1,\"channels\":null,\"metronome\":{\"enabled\":true,\"volume\":1,\"bpmOverride\":null,\"offsetMs\":0,\"beatsPerBar\":4}}}", "settings.channels")]
    [InlineData("{\"trackId\":\"0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a\",\"settings\":{\"name\":\"x\",\"masterVolume\":1,\"channels\":[null,{\"volume\":1}],\"metronome\":{\"enabled\":true,\"volume\":1,\"bpmOverride\":null,\"offsetMs\":0,\"beatsPerBar\":4}}}", "settings.channels[1].stem")]
    public async Task Create_MissingPartsReturn400(string json, string field)
    {
        await _factory.SeedAsync(SyncTestData.CompletedTrack());
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _client.PostAsync(new Uri(ApiRoutes.Mixes, UriKind.Relative), content, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync(field)).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task List_ReturnsSummariesNewestFirstAndFiltersByTrack()
    {
        var first = await _factory.SeedAsync(SyncTestData.CompletedTrack("aaaaaaaaaaa"));
        var second = await _factory.SeedAsync(SyncTestData.CompletedTrack("bbbbbbbbbbb"));
        var older = await (await CreateAsync(first.Id, Settings("Older"))).BodyAsync<MixDto>();
        _factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var newer = await (await CreateAsync(second.Id, Settings("Newer"))).BodyAsync<MixDto>();

        using var all = await _client.GetAsync(new Uri(ApiRoutes.Mixes, UriKind.Relative), Ct);
        using var filtered = await _client.GetAsync(new Uri($"{ApiRoutes.Mixes}?trackId={first.Id}", UriKind.Relative), Ct);

        var summaries = await all.BodyAsync<List<MixSummaryDto>>();
        summaries.Select(m => m.Id).ShouldBe([newer.Id, older.Id]);
        summaries[0].ShouldBe(new MixSummaryDto(newer.Id, second.Id, first.Title, "Newer", ApiFactory.Start.AddMinutes(1)));
        (await filtered.BodyAsync<List<MixSummaryDto>>()).Select(m => m.Id).ShouldBe([older.Id]);
    }

    [Fact]
    public async Task List_InvalidTrackIdReturns400()
    {
        using var response = await _client.GetAsync(new Uri($"{ApiRoutes.Mixes}?trackId=nope", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync("trackId")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Get_Returns200Or404()
    {
        var track = await _factory.SeedAsync(SyncTestData.CompletedTrack());
        var mix = await (await CreateAsync(track.Id, Settings())).BodyAsync<MixDto>();

        using var found = await _client.GetAsync(new Uri(ApiRoutes.Mix(mix.Id), UriKind.Relative), Ct);
        using var missing = await _client.GetAsync(new Uri(ApiRoutes.Mix(Guid.NewGuid()), UriKind.Relative), Ct);

        (await found.BodyAsync<MixDto>()).Settings.Channels.ShouldBe(mix.Settings.Channels);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_ReplacesSettingsAndAdvancesUpdatedAt()
    {
        var track = await _factory.SeedAsync(SyncTestData.CompletedTrack());
        var mix = await (await CreateAsync(track.Id, Settings())).BodyAsync<MixDto>();
        _factory.Clock.Advance(TimeSpan.FromMinutes(3));
        var changed = Settings("guitar PRACTICE") with { MasterVolume = 0.5, Channels = [], Loop = null };

        using var response = await _client.PutAsJsonAsync(new Uri(ApiRoutes.Mix(mix.Id), UriKind.Relative), changed, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.BodyAsync<MixDto>();
        updated.Settings.Name.ShouldBe("guitar PRACTICE");
        updated.Settings.MasterVolume.ShouldBe(0.5);
        updated.Settings.Channels.ShouldBeEmpty();
        updated.Settings.Loop.ShouldBeNull();
        updated.CreatedAt.ShouldBe(ApiFactory.Start);
        updated.UpdatedAt.ShouldBe(ApiFactory.Start.AddMinutes(3));
    }

    [Fact]
    public async Task Put_RenameClashReturns409AndKeepsTheMix()
    {
        var track = await _factory.SeedAsync(SyncTestData.CompletedTrack());
        (await CreateAsync(track.Id, Settings("Taken"))).Dispose();
        var mix = await (await CreateAsync(track.Id, Settings("Mine"))).BodyAsync<MixDto>();

        using var response = await _client.PutAsJsonAsync(new Uri(ApiRoutes.Mix(mix.Id), UriKind.Relative), Settings(" taken "), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var reloaded = await _client.GetAsync(new Uri(ApiRoutes.Mix(mix.Id), UriKind.Relative), Ct);
        (await reloaded.BodyAsync<MixDto>()).Settings.Name.ShouldBe("Mine");
    }

    [Fact]
    public async Task Put_InvalidReturns400WithBodyRelativeKeysAnd404ForUnknown()
    {
        var track = await _factory.SeedAsync(SyncTestData.CompletedTrack());
        var mix = await (await CreateAsync(track.Id, Settings())).BodyAsync<MixDto>();
        var invalid = Settings() with { Metronome = new MetronomeSettingsDto(true, 0.7, null, 5000, 4) };

        using var badRequest = await _client.PutAsJsonAsync(new Uri(ApiRoutes.Mix(mix.Id), UriKind.Relative), invalid, Ct);
        using var notFound = await _client.PutAsJsonAsync(new Uri(ApiRoutes.Mix(Guid.NewGuid()), UriKind.Relative), Settings(), Ct);
        using var empty = await _client.PutAsync(new Uri(ApiRoutes.Mix(mix.Id), UriKind.Relative), new StringContent("", Encoding.UTF8, "application/json"), Ct);

        badRequest.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await badRequest.FieldErrorsAsync("metronome.offsetMs")).ShouldNotBeEmpty();
        notFound.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        empty.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_Returns204ThenTheMixIsGone()
    {
        var track = await _factory.SeedAsync(SyncTestData.CompletedTrack());
        var mix = await (await CreateAsync(track.Id, Settings())).BodyAsync<MixDto>();

        using var deleted = await _client.DeleteAsync(new Uri(ApiRoutes.Mix(mix.Id), UriKind.Relative), Ct);
        using var again = await _client.DeleteAsync(new Uri(ApiRoutes.Mix(mix.Id), UriKind.Relative), Ct);
        using var get = await _client.GetAsync(new Uri(ApiRoutes.Mix(mix.Id), UriKind.Relative), Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        again.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _factory.FindTrackAsync(track.Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Duplicate_CreatesCopyThenCopy2()
    {
        var track = await _factory.SeedAsync(SyncTestData.CompletedTrack());
        var mix = await (await CreateAsync(track.Id, Settings())).BodyAsync<MixDto>();

        using var first = await DuplicateAsync(mix.Id);
        using var second = await DuplicateAsync(mix.Id);
        using var missing = await DuplicateAsync(Guid.NewGuid());

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        var copy = await first.BodyAsync<MixDto>();
        first.Headers.Location!.ToString().ShouldBe(ApiRoutes.Mix(copy.Id));
        copy.Settings.ShouldBe(mix.Settings with { Name = "Guitar practice (copy)", Channels = copy.Settings.Channels });
        copy.Settings.Channels.ShouldBe(mix.Settings.Channels);
        (await second.BodyAsync<MixDto>()).Settings.Name.ShouldBe("Guitar practice (copy 2)");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SavedMix_SurvivesAnApiRestart()
    {
        var dataDir = Directory.CreateTempSubdirectory("stemstudio-restart-").FullName;
        Guid mixId;
        using (var before = new ApiFactory(dataDir))
        using (var client = before.CreateClient())
        {
            var track = await before.SeedAsync(SyncTestData.CompletedTrack());
            using var created = await client.PostAsJsonAsync(new Uri(ApiRoutes.Mixes, UriKind.Relative), new CreateMixRequest(track.Id, Settings()), Ct);
            mixId = (await created.BodyAsync<MixDto>()).Id;
        }

        using var after = new ApiFactory(dataDir);
        using var restarted = after.CreateClient();
        using var response = await restarted.GetAsync(new Uri(ApiRoutes.Mix(mixId), UriKind.Relative), Ct);

        var mix = await response.BodyAsync<MixDto>();
        mix.Settings.Name.ShouldBe(Settings().Name);
        mix.Settings.Channels.ShouldBe(Settings().Channels);
        mix.Settings.Metronome.ShouldBe(Settings().Metronome);
        mix.Settings.Loop.ShouldBe(Settings().Loop);
        mix.Settings.MasterVolume.ShouldBe(Settings().MasterVolume);
    }

    internal Task<HttpResponseMessage> CreateAsync(Guid trackId, MixSettingsDto settings) =>
        _client.PostAsJsonAsync(new Uri(ApiRoutes.Mixes, UriKind.Relative), new CreateMixRequest(trackId, settings), Ct);

    private Task<HttpResponseMessage> DuplicateAsync(Guid mixId) =>
        _client.PostAsync(new Uri(ApiRoutes.DuplicateMix(mixId), UriKind.Relative), null, Ct);
}

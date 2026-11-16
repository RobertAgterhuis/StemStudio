using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Agent.Ports;
using StemStudio.Api.Agent.Loopback;
using StemStudio.Api.Tests.Mixes;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Styles;
using StemStudio.Domain.Voices;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.Agent;

/// <summary>
/// 011 T034: every <see cref="IStemStudioActions"/> member calls the public endpoint (the in-memory API behind a recording
/// handler) with the Contracts DTOs; songs and voices drop private text; problems become readable tool errors (FR-005).
/// </summary>
public sealed class LoopbackActionsTests : ApiTestBase
{
    private readonly RecordingHandler _http;
    private readonly LoopbackActions _actions;

    public LoopbackActionsTests()
    {
        _http = new RecordingHandler(Factory.Server.CreateHandler());
        _actions = new LoopbackActions(new LoopbackClient(new HttpClient(_http) { BaseAddress = new Uri("http://127.0.0.1:5000/") }));
    }

    public static TheoryData<string, string, string> Calls => new()
    {
        { "ImportFromUrl", "POST", "/api/imports" },
        { "RetryTrack", "POST", "/api/tracks/{id}/retry" },
        { "DeleteTrack", "DELETE", "/api/tracks/{id}" },
        { "StartDetail", "POST", "/api/tracks/{id}/detail" },
        { "CancelDetail", "POST", "/api/tracks/{id}/detail/cancel" },
        { "LookupMetadata", "POST", "/api/tracks/{id}/metadata/lookup" },
        { "SearchLyrics", "POST", "/api/tracks/{id}/lyrics/search" },
        { "AlignLyrics", "POST", "/api/tracks/{id}/lyrics/align" },
        { "RemoveLyrics", "DELETE", "/api/tracks/{id}/lyrics" },
        { "GetAsset", "GET", "/api/assets/{id}" },
        { "CreateAudioExport", "POST", "/api/assets/audio-exports" },
        { "CreateStemPack", "POST", "/api/assets/stem-packs" },
        { "DeleteAsset", "DELETE", "/api/assets/{id}" },
        { "GetScoreOptions", "GET", "/api/scores/options?trackId={id}&channel=bass" },
        { "CreateScore", "POST", "/api/assets/scores" },
        { "CreateRestyle", "POST", "/api/tracks/{id}/restyles" },
        { "GetGeneration", "GET", "/api/generations/{id}" },
        { "CancelGeneration", "POST", "/api/generations/{id}/cancel" },
        { "RetryGeneration", "POST", "/api/generations/{id}/retry" },
        { "DeleteGeneration", "DELETE", "/api/generations/{id}" },
        { "ImportTake", "POST", "/api/generations/{id}/takes/1/import" },
        { "CreateSong", "POST", "/api/songs" },
        { "CreateVoiceConversion", "POST", "/api/generations/{id}/takes/1/voice-conversions" },
        { "GetBandInfo", "GET", "/api/tracks/{id}/band-info" },
        { "GetLyricsSummary", "GET", "/api/tracks/{id}/lyrics" },
        { "GetMix", "GET", "/api/mixes/{id}" },
        { "UpdateMix", "PUT", "/api/mixes/{id}" },
        { "DuplicateMix", "POST", "/api/mixes/{id}/duplicate" },
        { "DeleteMix", "DELETE", "/api/mixes/{id}" },
        { "DeleteStyle", "DELETE", "/api/styles/{id}" },
        { "DeleteLyricsSheet", "DELETE", "/api/lyrics/{id}" },
    };

    [Theory]
    [MemberData(nameof(Calls))]
    public async Task UnknownIds_CallTheEndpointAndBecomeReadableToolErrors(string member, string method, string path)
    {
        var id = Guid.NewGuid();

        var error = await Should.ThrowAsync<AgentActionException>(() => Invoke(member, id));

        var request = _http.Requests.Single();
        (request.Method.Method, request.PathAndQuery).ShouldBe((method, path.Replace("{id}", id.ToString(), StringComparison.Ordinal)));
        error.StatusCode.ShouldBeInRange(400, 499);
        error.Message.ShouldNotBeNullOrWhiteSpace();
        error.Message.ShouldNotContain("http");
        error.Message.ShouldNotContain(" at ");
        error.Message.Length.ShouldBeLessThanOrEqualTo(500);
    }

    [Fact]
    public async Task NotFound_IsMarkedAsSuch()
    {
        var id = Guid.NewGuid();
        var error = await Should.ThrowAsync<AgentActionException>(() => _actions.Tracks.GetTrackAsync(id, Ct));

        (error.IsNotFound, error.ProblemCode, error.Message).ShouldBe((true, "not-found", $"Track {id} does not exist."));
    }

    [Fact]
    public async Task Tracks_ListGetAndDevice()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        (await _actions.Tracks.ListTracksAsync(Ct)).Single().Id.ShouldBe(track.Id);
        (await _actions.Tracks.GetTrackAsync(track.Id, Ct)).Title.ShouldBe(track.Title);
        (await _actions.Tracks.GetDeviceAsync(Ct)).ShouldNotBeNull();
        (await _actions.Lyrics.GetLyricsSummaryAsync(track.Id, Ct)).Instrumental.ShouldBeFalse();
        _http.Requests.Select(r => r.PathAndQuery).ShouldBe(["/api/tracks", $"/api/tracks/{track.Id}", "/api/device", $"/api/tracks/{track.Id}/lyrics"]);
    }

    [Fact]
    public async Task Mixes_CreateListUpdateDuplicateAndDelete()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        var mix = await _actions.Mixes.CreateMixAsync(new CreateMixRequest(track.Id, MixEndpointsTests.Settings()), Ct);
        (await _actions.Mixes.ListMixesAsync(track.Id, Ct)).Single().Id.ShouldBe(mix.Id);
        (await _actions.Mixes.ListMixesAsync(null, Ct)).Count.ShouldBe(1);
        (await _actions.Mixes.UpdateMixAsync(mix.Id, MixEndpointsTests.Settings("Renamed"), Ct)).Settings.Name.ShouldBe("Renamed");
        var copy = await _actions.Mixes.DuplicateMixAsync(mix.Id, Ct);
        await _actions.Mixes.DeleteMixAsync(copy.Id, Ct);
        (await _actions.Mixes.GetMixAsync(mix.Id, Ct)).Id.ShouldBe(mix.Id);
        _http.Requests[1].PathAndQuery.ShouldBe($"/api/mixes?trackId={track.Id}");
    }

    [Fact]
    public async Task Styles_ListCreateAndDelete()
    {
        var style = await _actions.Styles.CreateStyleAsync(new StyleInput
        {
            Name = "Night drive",
            GenreFamily = GenreFamilyDto.Electronic,
            TempoFeel = TempoFeelDto.Keep,
            VocalHandling = VocalHandlingDto.Keep,
            DefaultStrength = StrengthDto.Balanced,
        }, Ct);

        (await _actions.Styles.ListStylesAsync(new StyleQuery { Query = "night", Kind = StyleKindDto.User, GenreFamily = GenreFamilyDto.Electronic }, Ct))
            .Single().Id.ShouldBe(style.Id);
        _http.Requests[1].PathAndQuery.ShouldBe("/api/styles?q=night&kind=user&genreFamily=electronic");
        await _actions.Styles.DeleteStyleAsync(style.Id, Ct);
        (await _actions.Styles.ListStylesAsync(new StyleQuery(), Ct)).ShouldNotContain(s => s.Id == style.Id);
    }

    [Fact]
    public async Task ImitationRefusals_KeepTheReadableReason()
    {
        var error = await Should.ThrowAsync<AgentActionException>(() => _actions.Styles.CreateStyleAsync(new StyleInput
        {
            Name = "Like Taylor Swift",
            GenreFamily = GenreFamilyDto.Pop,
            TempoFeel = TempoFeelDto.Keep,
            VocalHandling = VocalHandlingDto.Keep,
            DefaultStrength = StrengthDto.Balanced,
            Description = "sound like Taylor Swift",
        }, Ct));

        error.StatusCode.ShouldBe(400);
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Songs_ListAndCreateLyricsSheetsWithoutTheirText()
    {
        var sheet = await _actions.Songs.CreateLyricsSheetAsync(new LyricsInput { Title = "Rain", Language = "en", Text = "secret words" }, Ct);

        var listed = (await _actions.Songs.ListLyricsSheetsAsync(Ct)).Single();
        (listed.LyricsId, listed.Title, listed.Language).ShouldBe((sheet.LyricsId, "Rain", "en"));
        JsonSerializer.Serialize(listed).ShouldNotContain("secret");
        JsonSerializer.Serialize(sheet).ShouldNotContain("secret");
        await _actions.Songs.DeleteLyricsSheetAsync(sheet.LyricsId, Ct);
        (await _actions.Songs.ListLyricsSheetsAsync(Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Voices_ListProfilesWithoutPersonalData()
    {
        var profile = await SeedProfileAsync();

        var listed = (await _actions.Voices.ListVoiceProfilesAsync(Ct)).Single();

        (listed.ProfileId, listed.DisplayName, listed.ConsentCurrent, listed.DurationSeconds).ShouldBe((profile.Id, "My voice", true, 21.4));
        var text = JsonSerializer.Serialize(listed);
        text.ShouldNotContain("Pat Example");
        text.ShouldNotContain("sample");
        typeof(IVoiceActions).GetMethods().Select(m => m.Name).ShouldBe(["ListVoiceProfilesAsync", "CreateVoiceConversionAsync"], ignoreOrder: true);
    }

    [Fact]
    public async Task GenerationsAndAssets_ListAndStatus()
    {
        (await _actions.Generations.GetStatusAsync(Ct)).ShouldNotBeNull();
        (await _actions.Generations.ListGenerationsAsync(new GenerationQuery { Kind = GenerationKindDto.Restyle, SourceTrackId = Guid.Empty }, Ct)).ShouldBeEmpty();
        (await _actions.Assets.ListAssetsAsync(new StemStudio.Agent.Ports.AssetQuery { Query = "live", Limit = 5 }, Ct)).Items.ShouldBeEmpty();
        _http.Requests.Select(r => r.PathAndQuery).ShouldBe(
            ["/api/generation/status", $"/api/generations?kind=restyle&sourceTrackId={Guid.Empty}", "/api/assets?q=live&limit=5"]);
    }

    [Fact]
    public async Task ServerErrorsAndUnreachableApi_AreGeneric()
    {
        var failing = new LoopbackActions(new LoopbackClient(new HttpClient(new StubHttpMessageHandler(_ =>
            Task.FromResult(StubHttpMessageHandler.Json(HttpStatusCode.InternalServerError, """{"title":"boom","detail":"at C:\\src\\x.cs line 3 http://10.0.0.1"}""", "application/problem+json"))))
        { BaseAddress = new Uri("http://127.0.0.1:5000/") }));
        var offline = new LoopbackActions(new LoopbackClient(new HttpClient(new StubHttpMessageHandler(_ => throw new HttpRequestException("refused http://127.0.0.1:5000")))
        { BaseAddress = new Uri("http://127.0.0.1:5000/") }));

        var server = await Should.ThrowAsync<AgentActionException>(() => failing.Tracks.ListTracksAsync(Ct));
        var unreachable = await Should.ThrowAsync<AgentActionException>(() => offline.Tracks.ListTracksAsync(Ct));

        (server.StatusCode, server.Message).ShouldBe((500, LoopbackClient.ServerErrorMessage));
        (unreachable.StatusCode, unreachable.Message).ShouldBe((0, LoopbackClient.UnreachableMessage));
    }

    [Fact]
    public void Registration_UsesTheConfiguredLoopbackBaseUrl()
    {
        using var factory = new ApiFactory { Settings = new Dictionary<string, string?> { ["StemStudio:Agent:LoopbackBaseUrl"] = "http://127.0.0.1:5123" } };

        factory.Services.GetRequiredService<IStemStudioActions>().ShouldBeOfType<LoopbackActions>();
        factory.Services.GetRequiredService<LoopbackClient>().BaseAddress.ShouldBe(new Uri("http://127.0.0.1:5123/"));
    }

    [Theory]
    [InlineData(null, "5001;5002", null, "http://127.0.0.1:5001/")]
    [InlineData(null, null, "8085", "http://127.0.0.1:8085/")]
    [InlineData(null, null, null, "http://127.0.0.1:8080/")]
    [InlineData("http://api.local:9000", "5001", null, "http://api.local:9000/")]
    public void LoopbackBaseUrl_FallsBackToTheHttpPorts(string? configured, string? httpPorts, string? aspNetPorts, string expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HTTP_PORTS"] = httpPorts,
            ["ASPNETCORE_HTTP_PORTS"] = aspNetPorts,
        }).Build();
        var options = new StemStudio.Api.Agent.AgentHostOptions { LoopbackBaseUrl = configured is null ? null : new Uri(configured) };

        LoopbackRegistration.BaseUrl(options, configuration).ShouldBe(new Uri(expected));
    }

    private async Task<VoiceProfile> SeedProfileAsync()
    {
        var profile = VoiceProfile.Create(Guid.NewGuid(), new VoiceProfileDraft(
            "My voice",
            new VoiceConsent(ConsentStatement.Version, ConsentStatement.Sha256, "Pat Example", ApiFactory.Start.UtcDateTime),
            new VoiceSample(VoiceSampleSource.Uploaded, "voices/p/sample.flac", new string('a', 64), new string('b', 64), 21.4, 0.78)), ApiFactory.Start.UtcDateTime);
        return await Factory.WithScopeAsync(async sp =>
        {
            await sp.GetRequiredService<IVoiceProfileRepository>().AddAsync(profile, Ct);
            return profile;
        });
    }

    private Task Invoke(string member, Guid id) => member switch
    {
        "ImportFromUrl" => _actions.Tracks.ImportFromUrlAsync("https://example.com/not-supported", Ct),
        "RetryTrack" => _actions.Tracks.RetryTrackAsync(id, Ct),
        "DeleteTrack" => _actions.Tracks.DeleteTrackAsync(id, Ct),
        "StartDetail" => _actions.Tracks.StartDetailAsync(id, Ct),
        "CancelDetail" => _actions.Tracks.CancelDetailAsync(id, Ct),
        "LookupMetadata" => _actions.Artists.LookupMetadataAsync(id, Ct),
        "GetBandInfo" => _actions.Artists.GetBandInfoAsync(id, Ct),
        "GetLyricsSummary" => _actions.Lyrics.GetLyricsSummaryAsync(id, Ct),
        "SearchLyrics" => _actions.Lyrics.SearchLyricsAsync(id, Ct),
        "AlignLyrics" => _actions.Lyrics.AlignLyricsAsync(id, Ct),
        "RemoveLyrics" => _actions.Lyrics.RemoveLyricsAsync(id, Ct),
        _ => InvokeLibrary(member, id),
    };

    private Task InvokeLibrary(string member, Guid id) => member switch
    {
        "GetAsset" => _actions.Assets.GetAssetAsync(id, Ct),
        "CreateAudioExport" => _actions.Assets.CreateAudioExportAsync(LoopbackRequests.AudioExport(id), Ct),
        "CreateStemPack" => _actions.Assets.CreateStemPackAsync(LoopbackRequests.StemPack(id), Ct),
        "DeleteAsset" => _actions.Assets.DeleteAssetAsync(id, Ct),
        "GetScoreOptions" => _actions.Assets.GetScoreOptionsAsync(id, "bass", Ct),
        "CreateScore" => _actions.Assets.CreateScoreAsync(LoopbackRequests.Score(id), Ct),
        "GetMix" => _actions.Mixes.GetMixAsync(id, Ct),
        "UpdateMix" => _actions.Mixes.UpdateMixAsync(id, MixEndpointsTests.Settings(), Ct),
        "DuplicateMix" => _actions.Mixes.DuplicateMixAsync(id, Ct),
        "DeleteMix" => _actions.Mixes.DeleteMixAsync(id, Ct),
        "DeleteStyle" => _actions.Styles.DeleteStyleAsync(id, Ct),
        _ => InvokeGeneration(member, id),
    };

    private Task InvokeGeneration(string member, Guid id) => member switch
    {
        "CreateRestyle" => _actions.Generations.CreateRestyleAsync(id, new RestyleRequest { StyleId = Guid.NewGuid(), Mode = RestyleModeDto.Range }, Ct),
        "GetGeneration" => _actions.Generations.GetGenerationAsync(id, Ct),
        "CancelGeneration" => _actions.Generations.CancelGenerationAsync(id, Ct),
        "RetryGeneration" => _actions.Generations.RetryGenerationAsync(id, Ct),
        "DeleteGeneration" => _actions.Generations.DeleteGenerationAsync(id, Ct),
        "ImportTake" => _actions.Generations.ImportTakeAsync(id, 1, Ct),
        "CreateSong" => _actions.Songs.CreateSongAsync(new SongRequest { LyricsId = id, Language = "en", FreeCaption = "pop", LeadVoice = LeadVoiceDto.Any }, Ct),
        "CreateVoiceConversion" => _actions.Voices.CreateVoiceConversionAsync(id, 1, new VoiceConversionRequest { ProfileId = Guid.NewGuid() }, Ct),
        "DeleteLyricsSheet" => _actions.Songs.DeleteLyricsSheetAsync(id, Ct),
        _ => throw new ArgumentOutOfRangeException(nameof(member), member, null),
    };

    private sealed class RecordingHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        public List<(HttpMethod Method, string PathAndQuery)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!.PathAndQuery));
            return base.SendAsync(request, cancellationToken);
        }
    }
}

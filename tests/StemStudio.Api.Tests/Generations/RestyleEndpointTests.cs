using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Storage;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Styles;
using StemStudio.Domain.Styles;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;
using StyleFields = StemStudio.Domain.Styles.StyleFields;

namespace StemStudio.Api.Tests.Generations;

/// <summary>009 T037: <c>POST /api/tracks/{trackId}/restyles</c> (FR-001, FR-002, FR-006).</summary>
public sealed class RestyleEndpointTests : ApiTestBase
{
    public static readonly Guid JazzTrio = Guid.Parse("3f9c2a1e-6b4d-4e8f-a0c2-1d3e5f7a9b01");

    [Fact]
    public async Task Create_SnapshotsTheStyleAndUploadsTheStems()
    {
        var track = await SeedTrackAsync(Factory);

        using var response = await PostAsync(track.Id, Request() with { Takes = 2 });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var generation = await response.BodyAsync<GenerationDto>();
        response.Headers.Location!.OriginalString.ShouldBe(ApiRoutes.Generation(generation.Id));
        (generation.Kind, generation.Status, generation.Progress, generation.Stage).ShouldBe((GenerationKindDto.Restyle, GenerationStateDto.Queued, 0, "Queued"));
        var restyle = generation.Restyle!;
        (restyle.SourceTrackId, restyle.SourceTrackTitle).ShouldBe((track.Id, track.Title));
        restyle.Range.ShouldBe(new SourceRangeDto { StartSeconds = 60, EndSeconds = 105, Mode = RestyleModeDto.Range });
        restyle.IncludedStems.ShouldBe(StemCatalog.SixStem);
        (restyle.Style.StyleId, restyle.Style.Kind, restyle.Style.PresetKey, restyle.Style.Name).ShouldBe((JazzTrio, StyleKindDto.Preset, "jazz-trio", "Jazz trio"));
        (restyle.Strength, restyle.Caption).ShouldBe((StrengthDto.Balanced, "Jazz swing, swing feel, piano, upright bass, brushed drums, relaxed, warm"));
        generation.Takes.ShouldBeEmpty();
        await ContractAssert.MatchesAsync(response, "createRestyle");
    }

    [Fact]
    public async Task Create_SendsTheBuiltParametersToTheGenerator()
    {
        var track = await SeedTrackAsync(Factory);

        (await PostAsync(track.Id, Request() with { Takes = 2 })).Dispose();

        var created = Factory.Generator.Created.Single();
        var restyle = created.Params["restyle"]!;
        created.Params["kind"]!.GetValue<string>().ShouldBe("restyle");
        restyle["caption"]!.GetValue<string>().ShouldBe("Jazz swing, swing feel, piano, upright bass, brushed drums, relaxed, warm");
        restyle["audioCoverStrength"]!.GetValue<double>().ShouldBe(0.55);
        (restyle["inferenceSteps"]!.GetValue<int>(), restyle["shift"]!.GetValue<double>()).ShouldBe((8, 3.0));
        restyle["seeds"]!.AsArray().Select(seed => seed!.GetValue<int>()).ShouldBe([1000, 2000]);
        (restyle["mode"]!.GetValue<string>(), restyle["rangeStartSeconds"]!.GetValue<double>(), restyle["rangeEndSeconds"]!.GetValue<double>()).ShouldBe(("range", 60.0, 105.0));
        created.Stems.Select(stem => stem.FileName).ShouldBe(StemCatalog.SixStem.Select(name => name + ".flac"));
        created.Stems[0].Bytes.ShouldBe(StemBytes("vocals"));
        var stored = await Factory.WithScopeAsync(sp => sp.GetRequiredService<IGenerationRepository>().ListAsync(null, null, CancellationToken.None));
        stored.Single().GeneratorJobId.ShouldBe(created.Id);
        stored.Single().Seeds.ShouldBe([1000, 2000]);
    }

    [Fact]
    public async Task Create_InstrumentalStyleLeavesOutTheVocalsAndStrengthOverrides()
    {
        var track = await SeedTrackAsync(Factory);
        var style = await SeedUserStyleAsync(Factory, VocalHandling.Instrumental);

        using var response = await PostAsync(track.Id, new RestyleRequest { StyleId = style.Id, Mode = RestyleModeDto.InPlace, RangeStartSeconds = 15, RangeEndSeconds = 30, Strength = StrengthDto.Strong });

        var restyle = (await response.BodyAsync<GenerationDto>()).Restyle!;
        restyle.IncludedStems.ShouldBe(["drums", "bass", "guitar", "piano", "other"]);
        restyle.Strength.ShouldBe(StrengthDto.Strong);
        restyle.Range.Mode.ShouldBe(RestyleModeDto.InPlace);
        restyle.Caption.ShouldEndWith("instrumental, Late-night trio.");
        Factory.Generator.Created.Single().Params["restyle"]!["audioCoverStrength"]!.GetValue<double>().ShouldBe(0.40);
    }

    [Fact]
    public async Task Create_ExplicitStemsAreSent()
    {
        var track = await SeedTrackAsync(Factory);

        (await PostAsync(track.Id, Request() with { IncludedStems = ["bass", "drums"] })).Dispose();

        Factory.Generator.Created.Single().Stems.Select(stem => stem.FileName).ShouldBe(["drums.flac", "bass.flac"]);
    }

    /// <summary>Range start and end, one included stem (or none), the take count, and the field each refusal names.</summary>
    public static TheoryData<(double Start, double End, string? Stem, int Takes), string> InvalidRequests => new()
    {
        { (60, 65, null, 1), "rangeEndSeconds" },
        { (0, 600, null, 1), "rangeEndSeconds" },
        { (60, 105, "kazoo", 1), "includedStems" },
        { (60, 105, null, 3), "takes" },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Create_InvalidRequestsAre400((double Start, double End, string? Stem, int Takes) bad, string field)
    {
        var track = await SeedTrackAsync(Factory);
        var request = Request() with { RangeStartSeconds = bad.Start, RangeEndSeconds = bad.End, Takes = bad.Takes, IncludedStems = bad.Stem is null ? null : [bad.Stem] };

        using var response = await PostAsync(track.Id, request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync(field)).ShouldNotBeEmpty();
        Factory.Generator.Created.ShouldBeEmpty();
        await ContractAssert.MatchesAsync(response, "createRestyle");
    }

    [Fact]
    public async Task Create_UnknownStyleIs400()
    {
        var track = await SeedTrackAsync(Factory);

        using var response = await PostAsync(track.Id, Request() with { StyleId = Guid.NewGuid() });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync("styleId")).ShouldBe(["The style does not exist."]);
    }

    [Fact]
    public async Task Create_UnknownTrackIs404()
    {
        using var response = await PostAsync(Guid.NewGuid(), Request());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(response, "createRestyle");
    }

    [Fact]
    public async Task Create_TrackNotCompletedIs409()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());

        using var response = await PostAsync(track.Id, Request());

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "track-not-ready");
    }

    [Fact]
    public async Task Create_GeneratorWithoutGpuIs409WithTheReason()
    {
        Factory.Generator.HealthExample = "NoGpuHealth";
        var track = await SeedTrackAsync(Factory);

        using var response = await PostAsync(track.Id, Request());

        var problem = await AssertProblemAsync(response, HttpStatusCode.Conflict, "gpu-required");
        problem.GetProperty("detail").GetString().ShouldBe("CUDA is not available (no NVIDIA GPU or driver visible in the container).");
        Factory.Generator.Created.ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_GeneratorNotInstalledIs409()
    {
        using var factory = new ApiFactory { GeneratorInstalled = false };
        using var client = factory.CreateClient();
        var track = await SeedTrackAsync(factory);

        using var response = await client.PostAsJsonAsync(new Uri(ApiRoutes.Restyles(track.Id), UriKind.Relative), Request(), Ct);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "not-installed");
    }

    [Fact]
    public async Task Create_GeneratorUnreachableIs409NotInstalled()
    {
        Factory.Generator.Override = request => request.RequestUri!.AbsolutePath == "/health" ? throw new HttpRequestException("refused") : null;
        var track = await SeedTrackAsync(Factory);

        using var response = await PostAsync(track.Id, Request());

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "not-installed");
    }

    [Fact]
    public async Task Create_UploadFailureIs503AndKeepsNothing()
    {
        Factory.Generator.Override = request => request.Method == HttpMethod.Post ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : null;
        var track = await SeedTrackAsync(Factory);

        using var response = await PostAsync(track.Id, Request());

        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, "generator-unavailable");
        (await Factory.WithScopeAsync(sp => sp.GetRequiredService<IGenerationRepository>().ListAsync(null, null, CancellationToken.None))).ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_GeneratorRefusingForTheGpuIs409()
    {
        Factory.Generator.Override = request => request.Method == HttpMethod.Post ? GeneratorExamples.Problem(HttpStatusCode.Conflict, "gpuRequired", "GPU went away.") : null;
        var track = await SeedTrackAsync(Factory);

        using var response = await PostAsync(track.Id, Request());

        (await AssertProblemAsync(response, HttpStatusCode.Conflict, "gpu-required")).GetProperty("detail").GetString().ShouldBe("GPU went away.");
    }

    [Fact]
    public async Task Create_GeneratorRefusingTheInputIs400()
    {
        Factory.Generator.Override = request => request.Method == HttpMethod.Post ? GeneratorExamples.Problem(HttpStatusCode.UnprocessableEntity, "sourceTooShort", "The source is shorter than the range.") : null;
        var track = await SeedTrackAsync(Factory);

        using var response = await PostAsync(track.Id, Request());

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync("rangeEndSeconds")).ShouldBe(["The source is shorter than the range."]);
    }

    internal static RestyleRequest Request() => new() { StyleId = JazzTrio, Mode = RestyleModeDto.Range, RangeStartSeconds = 60, RangeEndSeconds = 105 };

    internal static byte[] StemBytes(string stem) => System.Text.Encoding.UTF8.GetBytes("flac:" + stem);

    /// <summary>A completed six-stem track with stem files on disk.</summary>
    internal static async Task<Track> SeedTrackAsync(ApiFactory factory, string videoId = "dQw4w9WgXcQ")
    {
        var track = await factory.SeedAsync(SyncTestData.CompletedTrack(videoId));
        var stems = factory.Services.GetRequiredService<IStemFileStore>();
        foreach (var stem in StemCatalog.SixStem)
        {
            await stems.SaveStemAsync(track.Id, stem, (s, ct) => s.WriteAsync(StemBytes(stem), ct).AsTask(), CancellationToken.None);
        }

        return track;
    }

    internal static Task<Style> SeedUserStyleAsync(ApiFactory factory, VocalHandling vocals = VocalHandling.Keep, string name = "Rainy cafe jazz") => factory.WithScopeAsync(async sp =>
    {
        var style = Style.CreateUser(Guid.NewGuid(), new StyleFields
        {
            Name = name,
            GenreFamily = GenreFamily.Jazz,
            SubGenre = "cool jazz",
            TempoFeel = TempoFeel.Laidback,
            Instrumentation = ["muted trumpet", "piano"],
            Moods = ["melancholic"],
            VocalHandling = vocals,
            DefaultStrength = Strength.Light,
            Description = "Late-night trio.",
        }, SyncTestData.T0);
        await sp.GetRequiredService<IStyleRepository>().AddAsync(style, CancellationToken.None);
        return style;
    });

    internal static async Task<System.Text.Json.JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string type)
    {
        response.StatusCode.ShouldBe(status);
        var problem = await response.JsonAsync();
        problem.GetProperty("type").GetString().ShouldBe(type);
        return problem;
    }

    private Task<HttpResponseMessage> PostAsync(Guid trackId, RestyleRequest request) =>
        Client.PostAsJsonAsync(new Uri(ApiRoutes.Restyles(trackId), UriKind.Relative), request, Ct);
}

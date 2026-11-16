using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.Generations;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Domain.Lyrics;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.Songs;

/// <summary>010 T027: <c>POST /api/songs</c> (FR-001, FR-010, FR-012).</summary>
public sealed class SongEndpointTests : ApiTestBase
{
    [Fact]
    public async Task Create_WithASheetAndAStyle()
    {
        var sheet = await SeedSheetAsync(Factory);

        using var response = await PostAsync(new SongRequest { LyricsId = sheet.Id, Language = "en", StyleId = RestyleEndpointTests.JazzTrio, LeadVoice = LeadVoiceDto.Female, Delivery = "warm", DurationSeconds = 60 });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var generation = await response.BodyAsync<GenerationDto>();
        response.Headers.Location!.OriginalString.ShouldBe(ApiRoutes.Generation(generation.Id));
        (generation.Kind, generation.Restyle, generation.Song!.Instrumental).ShouldBe((GenerationKindDto.Song, null, false));
        generation.Song.Lyrics.ShouldBe(new LyricsSnapshotDto { LyricsId = sheet.Id, Title = "This little song", Language = "en", Text = sheet.Text });
        generation.Song.Style!.PresetKey.ShouldBe("jazz-trio");
        generation.Song.Voice.ShouldBe(new SongVoiceDto { LeadVoice = LeadVoiceDto.Female, Delivery = "warm" });
        generation.Song.Caption.ShouldBe("Jazz swing, swing feel, piano, upright bass, brushed drums, relaxed, warm, female lead vocal, warm");
        var song = Factory.Generator.Created.Single().Params["song"]!;
        (song["lyrics"]!.GetValue<string>(), song["vocalLanguage"]!.GetValue<string>(), song["instrumental"]!.GetValue<bool>()).ShouldBe((sheet.Text, "en", false));
        song["seeds"]!.AsArray().Select(seed => seed!.GetValue<int>()).ShouldBe([1000]);
        await ContractAssert.MatchesAsync(response, "createSong");
    }

    [Fact]
    public async Task Create_InlineTextIsNormalisedWithAFreeCaption()
    {
        using var response = await PostAsync(new SongRequest
        {
            LyricsText = "[00:01.00]la la la\r\n",
            Title = "Mine",
            Language = "nl",
            FreeCaption = "pop rock, electric guitars, drums",
            LeadVoice = LeadVoiceDto.Male,
            Bpm = 120,
            KeyScale = "F# major",
            TimeSignature = "3",
            Takes = 2,
        });

        var song = (await response.BodyAsync<GenerationDto>()).Song!;
        (song.Lyrics.LyricsId, song.Lyrics.Title, song.Lyrics.Text, song.Style).ShouldBe((null, "Mine", "la la la", null));
        (song.Caption, song.Bpm, song.KeyScale, song.TimeSignature, song.DurationSeconds).ShouldBe(("pop rock, electric guitars, drums, male lead vocal", (int?)120, "F# major", "3", 120.0));
        Factory.Generator.Created.Single().Params["song"]!["seeds"]!.AsArray().Count.ShouldBe(2);
    }

    [Fact]
    public async Task Create_InstrumentalSendsTheInstrumentalMarker()
    {
        using var response = await PostAsync(new SongRequest
        {
            LyricsText = string.Empty,
            Title = "Bossa backing",
            Language = "unknown",
            FreeCaption = "bossa nova, nylon guitar",
            LeadVoice = LeadVoiceDto.Any,
            Instrumental = true,
            DurationSeconds = 30,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await response.BodyAsync<GenerationDto>()).Song!.Caption.ShouldBe("bossa nova, nylon guitar, instrumental");
        Factory.Generator.Created.Single().Params["song"]!["lyrics"]!.GetValue<string>().ShouldBe("[Instrumental]");
    }

    [Fact]
    public async Task Create_DenseLyricsGiveAWarningNotAnError()
    {
        var words = string.Join(' ', Enumerable.Repeat("word", 50));

        using var response = await PostAsync(new SongRequest { LyricsText = words, Title = "Fast", Language = "en", LeadVoice = LeadVoiceDto.Any, DurationSeconds = 10 });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await response.BodyAsync<GenerationDto>()).Warnings.Single().ShouldStartWith("The lyrics have 5.0 words per second");
        await ContractAssert.MatchesAsync(response, "createSong");
    }

    [Theory]
    [InlineData("freeCaption")]
    [InlineData("delivery")]
    public async Task Create_ImitationIsRefused(string field)
    {
        var request = new SongRequest
        {
            LyricsText = "la",
            Title = "t",
            Language = "en",
            LeadVoice = LeadVoiceDto.Any,
            FreeCaption = field == "freeCaption" ? "Upbeat pop in the style of a chart singer" : null,
            Delivery = field == "delivery" ? "sounds like a famous tenor" : null,
        };

        using var response = await PostAsync(request);

        await RestyleEndpointTests.AssertProblemAsync(response, HttpStatusCode.BadRequest, "imitation");
        (await response.FieldErrorsAsync(field)).ShouldNotBeEmpty();
        await ContractAssert.MatchesAsync(response, "createSong");
    }

    [Theory]
    [InlineData(null, null, "lyricsText")]
    [InlineData("both", null, "lyricsId")]
    [InlineData(null, "xx", "language")]
    [InlineData("long", null, "lyricsText")]
    public async Task Create_InvalidLyricsAre400(string? lyrics, string? language, string field)
    {
        var request = new SongRequest
        {
            LyricsId = lyrics == "both" ? Guid.NewGuid() : null,
            LyricsText = lyrics switch { "both" => "x", "long" => new string('a', 5000), _ => language is null ? null : "la" },
            Language = language ?? "en",
            LeadVoice = LeadVoiceDto.Any,
        };

        using var response = await PostAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync(field)).ShouldNotBeEmpty();
        await ContractAssert.MatchesAsync(response, "createSong");
    }

    [Fact]
    public async Task Create_OptionsAndStyleXorCaptionAreValidated()
    {
        using var options = await PostAsync(new SongRequest { LyricsText = "la", Language = "en", LeadVoice = LeadVoiceDto.Any, Bpm = 300 });
        using var both = await PostAsync(new SongRequest { LyricsText = "la", Language = "en", LeadVoice = LeadVoiceDto.Any, StyleId = RestyleEndpointTests.JazzTrio, FreeCaption = "pop" });

        (await options.FieldErrorsAsync("bpm")).ShouldNotBeEmpty();
        (await both.FieldErrorsAsync("freeCaption")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Create_UnknownSheetOrStyleIs404()
    {
        using var sheet = await PostAsync(new SongRequest { LyricsId = Guid.NewGuid(), Language = "en", LeadVoice = LeadVoiceDto.Any });
        using var style = await PostAsync(new SongRequest { LyricsText = "la", Language = "en", LeadVoice = LeadVoiceDto.Any, StyleId = Guid.NewGuid() });

        (sheet.StatusCode, style.StatusCode).ShouldBe((HttpStatusCode.NotFound, HttpStatusCode.NotFound));
        await ContractAssert.MatchesAsync(sheet, "createSong");
    }

    [Fact]
    public async Task Create_NotReadyIs409AndAnOutageIs503()
    {
        Factory.Generator.HealthExample = "NoGpuHealth";
        using var notReady = await PostAsync(new SongRequest { LyricsText = "la", Language = "en", LeadVoice = LeadVoiceDto.Any });
        Factory.Generator.HealthExample = "ReadyHealth";
        Factory.Clock.Advance(TimeSpan.FromSeconds(11));
        Factory.Generator.Override = request => request.Method == HttpMethod.Post ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : null;
        using var down = await PostAsync(new SongRequest { LyricsText = "la", Language = "en", LeadVoice = LeadVoiceDto.Any });

        await RestyleEndpointTests.AssertProblemAsync(notReady, HttpStatusCode.Conflict, "gpu-required");
        await RestyleEndpointTests.AssertProblemAsync(down, HttpStatusCode.ServiceUnavailable, "generator-unavailable");
        await ContractAssert.MatchesAsync(notReady, "createSong");
        await ContractAssert.MatchesAsync(down, "createSong");
    }

    internal static Task<LyricsSheet> SeedSheetAsync(ApiFactory factory) => factory.WithScopeAsync(async sp =>
    {
        var sheet = LyricsSheet.Create(Guid.NewGuid(), new LyricsContent("This little song", "en", "[verse]\nSing it loud, sing it free\n\n[chorus]\nThis little song belongs to me"), SyncTestData.T0);
        await sp.GetRequiredService<ILyricsSheetRepository>().AddAsync(sheet, CancellationToken.None);
        return sheet;
    });

    private Task<HttpResponseMessage> PostAsync(SongRequest request) => Client.PostAsJsonAsync(new Uri(ApiRoutes.Songs, UriKind.Relative), request, Ct);
}

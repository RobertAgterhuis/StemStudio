using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Generator;
using StemStudio.Api.Tests.TestSupport;

namespace StemStudio.Api.Tests.Generator;

/// <summary>010 T020: songs, voice conversions, voice-sample checks and the singer client against the 1.1.0 examples.</summary>
public sealed class GeneratorClientSongVoiceTests : IDisposable
{
    private const string Id = GeneratorExamples.GenerationId;
    private readonly string _tempDir = Directory.CreateTempSubdirectory("stemstudio-singer-").FullName;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_tempDir, true);

    [Fact]
    public async Task CreateSong_SendsJsonWithoutAudio()
    {
        string? json = null;
        var (client, handler) = GeneratorClientTests.Create(request =>
        {
            request.Content!.Headers.ContentType!.MediaType.ShouldBe("application/json");
            json = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return GeneratorExamples.Response(HttpStatusCode.Accepted, "QueuedSong");
        });

        var job = await client.CreateSongAsync(SongParams(), Ct);

        (job.Kind, job.Status).ShouldBe(("song", GeneratorJobStatus.Queued));
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe("/v1/generations");
        JsonNode.DeepEquals(JsonNode.Parse(json!), JsonNode.Parse(GeneratorExamples.Read("SongRequest"))).ShouldBeTrue(json);
    }

    [Fact]
    public async Task CreateSong_InstrumentalExample()
    {
        string? json = null;
        var (client, _) = GeneratorClientTests.Create(request =>
        {
            json = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return GeneratorExamples.Response(HttpStatusCode.Accepted, "QueuedSong");
        });
        var parameters = new GeneratorSongParams(
            "Bossa nova, laidback feel, nylon guitar, soft percussion, flute, breezy, instrumental", "[Instrumental]", true, "unknown", 96, "D minor", "4", 30, [7]);

        await client.CreateSongAsync(parameters, Ct);

        JsonNode.DeepEquals(JsonNode.Parse(json!), JsonNode.Parse(GeneratorExamples.Read("InstrumentalSongRequest"))).ShouldBeTrue(json);
    }

    [Fact]
    public async Task CreateSong_KindNotServedIsMapped()
    {
        var (client, _) = GeneratorClientTests.Create(_ => GeneratorExamples.Problem(HttpStatusCode.Conflict, "kindNotServed", "This instance serves voiceConversion."));

        (await Should.ThrowAsync<GeneratorRequestException>(() => client.CreateSongAsync(SongParams(), Ct))).Code.ShouldBe(GeneratorProblemCode.KindNotServed);
    }

    [Fact]
    public async Task CreateVoiceConversion_SendsTargetPromptAndAccompaniment()
    {
        var parts = new List<(string Name, string? File, string Body)>();
        var (client, _) = GeneratorClientTests.Create(request =>
        {
            foreach (var part in request.Content.ShouldBeOfType<MultipartFormDataContent>())
            {
                parts.Add((part.Headers.ContentDisposition!.Name!.Trim('"'), part.Headers.ContentDisposition.FileName?.Trim('"'), part.ReadAsStringAsync().GetAwaiter().GetResult()));
            }

            return GeneratorExamples.Response(HttpStatusCode.Accepted, "QueuedSong");
        });
        var accompaniment = new[] { "drums", "bass", "guitar", "piano", "other" };
        var request = new GeneratorVoiceConversionRequest(
            new GeneratorVoiceConversionParams(0, true, "My voice"), await FileAsync("vocals"), await FileAsync("sample"), await Task.WhenAll(accompaniment.Select(FileAsync)));

        await client.CreateVoiceConversionAsync(request, Ct);

        parts.Select(part => part.Name).ShouldBe(["params", "target", "prompt", "accompaniment", "accompaniment", "accompaniment", "accompaniment", "accompaniment"]);
        JsonNode.DeepEquals(JsonNode.Parse(parts[0].Body), JsonNode.Parse(GeneratorExamples.Read("VoiceConversionParams"))).ShouldBeTrue(parts[0].Body);
        (parts[1].File, parts[1].Body, parts[2].File, parts[2].Body).ShouldBe(("vocals.flac", "vocals", "sample.flac", "sample"));
        parts.Skip(3).Select(part => part.File).ShouldBe(accompaniment.Select(name => name + ".flac"));
    }

    [Theory]
    [InlineData("VoiceSampleOk", true, null)]
    [InlineData("VoiceSampleUnvoiced", false, "unvoiced")]
    public async Task CheckVoiceSample_PostsTheSampleAndParsesTheCheck(string example, bool ok, string? reason)
    {
        var (client, handler) = GeneratorClientTests.Create(request =>
        {
            var part = request.Content.ShouldBeOfType<MultipartFormDataContent>().Single();
            part.Headers.ContentDisposition!.Name!.Trim('"').ShouldBe("sample");
            return GeneratorExamples.Response(HttpStatusCode.OK, example);
        });
        await using var sample = new MemoryStream([1, 2, 3]);

        var check = await client.CheckVoiceSampleAsync(sample, "take.webm", Ct);

        (check.Ok, check.Reason).ShouldBe((ok, reason));
        check.CheckId.Length.ShouldBe(32);
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe("/v1/voice-samples/check");
    }

    [Fact]
    public async Task CheckedSampleAndTakeVocal_AreDownloaded()
    {
        var (client, handler) = GeneratorClientTests.Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([7, 8]) });
        await using var sample = new MemoryStream();
        await using var vocal = new MemoryStream();

        await client.DownloadCheckedSampleAsync("2e4c6a8b0d1f43a5b7c9e1d3f5a7b9c1", sample, Ct);
        await client.DownloadTakeVocalAsync(Id, 1, vocal, Ct);

        sample.ToArray().ShouldBe([7, 8]);
        vocal.ToArray().ShouldBe([7, 8]);
        handler.Requests.Select(r => r.Uri.AbsolutePath).ShouldBe(["/v1/voice-samples/2e4c6a8b0d1f43a5b7c9e1d3f5a7b9c1/audio", $"/v1/generations/{Id}/takes/1/vocal"]);
        await Should.ThrowAsync<ArgumentException>(() => client.DownloadCheckedSampleAsync("../x", sample, Ct));
    }

    [Fact]
    public async Task Health_ReadsKindsAndTheSampleCheckFlag()
    {
        var (client, _) = GeneratorClientTests.Create(_ => GeneratorExamples.Response(HttpStatusCode.OK, "SingerReadyHealth"));

        var health = await client.GetHealthAsync(Ct);

        (health.Engine, health.VoiceSampleCheck).ShouldBe(("soulx", true));
        health.Kinds.ShouldBe(["voiceConversion"]);
        health.Serves("voiceConversion").ShouldBeTrue();
        health.Serves("song").ShouldBeFalse();
    }

    [Fact]
    public void Registration_TheSingerHasItsOwnBaseUrl()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["StemStudio:GeneratorBaseUrl"] = "http://generator:8010",
            ["StemStudio:SingerBaseUrl"] = "http://singer:8010",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGeneratorClient(configuration);
        services.AddSingerClient(configuration);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IGeneratorClient>().IsConfigured.ShouldBeTrue();
        provider.GetRequiredService<ISingerClient>().IsConfigured.ShouldBeTrue();
        provider.GetRequiredService<IHttpClientFactory>().ShouldNotBeNull();
    }

    [Fact]
    public void Registration_WithoutSingerUrlIsNotConfigured()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingerClient(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ISingerClient>().IsConfigured.ShouldBeFalse();
    }

    private static GeneratorSongParams SongParams() => new(
        "Pop rock, driving feel, electric guitars, bass, drums, upbeat, female lead vocal, warm",
        "[verse]\nSing it loud, sing it free\nEvery note is part of me\n\n[chorus]\nThis little song belongs to me",
        false, "en", null, null, null, 60, [42]);

    private async Task<string> FileAsync(string name)
    {
        var path = Path.Combine(_tempDir, name + ".flac");
        await File.WriteAllTextAsync(path, name, Ct);
        return path;
    }
}

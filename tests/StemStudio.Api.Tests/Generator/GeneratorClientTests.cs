using System.Net;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using StemStudio.Api.Generator;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Domain.Generations;

namespace StemStudio.Api.Tests.Generator;

/// <summary>009 T030: the typed generator client against the generator 1.0.0 examples (copies of the 1.1.0 document).</summary>
public sealed class GeneratorClientTests : IDisposable
{
    private const string Id = GeneratorExamples.GenerationId;
    private readonly string _tempDir = Directory.CreateTempSubdirectory("stemstudio-generator-").FullName;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_tempDir, true);

    [Fact]
    public async Task GetHealth_ParsesTheReadyExample()
    {
        var (client, handler) = Create(_ => GeneratorExamples.Response(HttpStatusCode.OK, "ReadyHealth"));

        var health = await client.GetHealthAsync(Ct);

        (health.Ready, health.Reason, health.Engine, health.ContractVersion).ShouldBe((true, null, "acestep", "1.1.0"));
        health.Kinds.ShouldBe(["restyle", "song"]);
        health.Device.Kind.ShouldBe("gpu");
        health.Device.Name.ShouldBe("NVIDIA GeForce RTX 5070 Ti");
        health.Device.TotalMemoryMiB.ShouldBe(16303);
        health.Models.Count.ShouldBe(4);
        health.Models[0].ShouldBe(new GeneratorModel("acestep-v15-turbo", false, 4_790_000_000, "19671f406d603126926c1b7e2adc169acbcade22"));
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe("/health");
    }

    [Fact]
    public async Task GetHealth_ParsesTheNoGpuExample()
    {
        var (client, _) = Create(_ => GeneratorExamples.Response(HttpStatusCode.OK, "NoGpuHealth"));

        var health = await client.GetHealthAsync(Ct);

        health.Ready.ShouldBeFalse();
        health.Reason.ShouldBe("CUDA is not available (no NVIDIA GPU or driver visible in the container).");
        health.Device.Kind.ShouldBe("cpu");
    }

    [Fact]
    public async Task GetHealth_UnreachableIsUnavailable()
    {
        var (client, _) = Create(_ => throw new HttpRequestException("refused"));

        await Should.ThrowAsync<GeneratorUnavailableException>(() => client.GetHealthAsync(Ct));
    }

    [Fact]
    public async Task GetHealth_GivesUpAfterTwoSeconds()
    {
        var clock = new FakeTimeProvider();
        var handler = new HangingHandler();
        var client = new GeneratorClient(new HttpClient(handler) { BaseAddress = new Uri("http://generator:8010/") }, NullLogger<GeneratorClient>.Instance, clock);

        var health = client.GetHealthAsync(Ct);
        await handler.Started.Task;
        clock.Advance(TimeSpan.FromSeconds(2));

        (await Should.ThrowAsync<GeneratorUnavailableException>(() => health)).Message.ShouldBe("The generator service did not answer in time.");
    }

    [Fact]
    public async Task EveryCall_WithoutABaseUrlIsNotInstalled()
    {
        var client = new GeneratorClient(new HttpClient(new HangingHandler()), NullLogger<GeneratorClient>.Instance, TimeProvider.System);

        client.IsConfigured.ShouldBeFalse();
        await Should.ThrowAsync<GeneratorNotInstalledException>(() => client.GetHealthAsync(Ct));
        await Should.ThrowAsync<GeneratorNotInstalledException>(() => client.GetAsync(Id, Ct));
    }

    [Fact]
    public async Task CreateRestyle_StreamsParamsAndStemsAsMultipart()
    {
        var parts = new List<(string Name, string? FileName, byte[] Bytes, string? Type)>();
        var (client, handler) = Create(request =>
        {
            foreach (var part in request.Content.ShouldBeOfType<MultipartFormDataContent>())
            {
                parts.Add((part.Headers.ContentDisposition!.Name!.Trim('"'), part.Headers.ContentDisposition.FileName?.Trim('"'),
                    part.ReadAsByteArrayAsync().GetAwaiter().GetResult(), part.Headers.ContentType?.MediaType));
            }

            return GeneratorExamples.Response(HttpStatusCode.Accepted, "QueuedRestyle");
        });
        var stems = new[] { await StemAsync("drums", [1, 2]), await StemAsync("bass", [3]) };

        var job = await client.CreateRestyleAsync(new GeneratorRestyleRequest(RangeParams(), stems), Ct);

        job.GenerationId.ShouldBe(Id);
        job.Status.ShouldBe(GeneratorJobStatus.Queued);
        var request = handler.Requests.Single();
        (request.Method, request.Uri.AbsolutePath).ShouldBe((HttpMethod.Post, "/v1/generations"));
        parts.Select(p => p.Name).ShouldBe(["params", "stem", "stem"]);
        parts[0].Type.ShouldBe("application/json");
        JsonNode.DeepEquals(JsonNode.Parse(parts[0].Bytes), JsonNode.Parse(GeneratorExamples.Read("RangeRestyleParams"))).ShouldBeTrue();
        (parts[1].FileName, parts[1].Bytes, parts[1].Type).ShouldBe(("drums.flac", new byte[] { 1, 2 }, "audio/flac"));
        (parts[2].FileName, parts[2].Bytes).ShouldBe(("bass.flac", new byte[] { 3 }));
    }

    [Fact]
    public async Task CreateRestyle_SendsTheInPlaceParams()
    {
        string? json = null;
        var (client, _) = Create(request =>
        {
            json = request.Content.ShouldBeOfType<MultipartFormDataContent>().First().ReadAsStringAsync().GetAwaiter().GetResult();
            return GeneratorExamples.Response(HttpStatusCode.Accepted, "QueuedRestyle");
        });
        var restyle = new GeneratorRestyleParams(
            "Reggae, laidback feel, offbeat guitar skank, organ, deep bass, one-drop drums, sunny, mellow", 0.55, 8, 3.0, [42], RestyleMode.InPlace, 15, 30);

        await client.CreateRestyleAsync(new GeneratorRestyleRequest(restyle, [await StemAsync("other", [9])]), Ct);

        JsonNode.DeepEquals(JsonNode.Parse(json!), JsonNode.Parse(GeneratorExamples.Read("InPlaceRestyleParams"))).ShouldBeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "gpuRequired", GeneratorProblemCode.GpuRequired)]
    [InlineData(HttpStatusCode.UnprocessableEntity, "sourceTooShort", GeneratorProblemCode.SourceTooShort)]
    [InlineData(HttpStatusCode.BadRequest, "invalidInput", GeneratorProblemCode.InvalidInput)]
    [InlineData(HttpStatusCode.Conflict, "kindNotServed", GeneratorProblemCode.KindNotServed)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, null, null)]
    [InlineData(HttpStatusCode.Conflict, "somethingNew", null)]
    public async Task CreateRestyle_MapsProblemBodies(HttpStatusCode status, string? code, GeneratorProblemCode? expected)
    {
        var (client, _) = Create(_ => GeneratorExamples.Problem(status, code, "The reason."));

        var error = await Should.ThrowAsync<GeneratorRequestException>(async () => await client.CreateRestyleAsync(new GeneratorRestyleRequest(RangeParams(), [await StemAsync("drums", [1])]), Ct));

        (error.StatusCode, error.Code, error.Detail).ShouldBe((status, expected, "The reason."));
    }

    [Fact]
    public async Task CreateRestyle_ProblemWithoutBodyUsesTheReason()
    {
        var (client, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.Conflict) { ReasonPhrase = "Conflict" });

        var error = await Should.ThrowAsync<GeneratorRequestException>(async () => await client.CreateRestyleAsync(new GeneratorRestyleRequest(RangeParams(), [await StemAsync("drums", [1])]), Ct));

        (error.Code, error.Detail).ShouldBe((null, "Conflict"));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.Accepted, "not json")]
    [InlineData(HttpStatusCode.Accepted, "null")]
    public async Task CreateRestyle_ServerErrorsAndBadBodiesAreUnavailable(HttpStatusCode status, string body)
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Json(status, body));

        await Should.ThrowAsync<GeneratorUnavailableException>(async () => await client.CreateRestyleAsync(new GeneratorRestyleRequest(RangeParams(), [await StemAsync("drums", [1])]), Ct));
    }

    [Theory]
    [InlineData("QueuedRestyle", GeneratorJobStatus.Queued)]
    [InlineData("WaitingForGpuRestyle", GeneratorJobStatus.WaitingForGpu)]
    [InlineData("RunningRestyle", GeneratorJobStatus.Running)]
    [InlineData("CompletedRestyle", GeneratorJobStatus.Completed)]
    [InlineData("FailedOutOfMemory", GeneratorJobStatus.Failed)]
    [InlineData("CancelledRestyle", GeneratorJobStatus.Cancelled)]
    public async Task Get_ParsesEveryJobExample(string example, GeneratorJobStatus status)
    {
        var (client, handler) = Create(_ => GeneratorExamples.Response(HttpStatusCode.OK, example));

        var job = (await client.GetAsync(Id, Ct))!;

        job.Status.ShouldBe(status);
        job.Kind.ShouldBe("restyle");
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/generations/{Id}");
    }

    [Fact]
    public async Task Get_CompletedCarriesTakesAndDevice()
    {
        var (client, _) = Create(_ => GeneratorExamples.Response(HttpStatusCode.OK, "CompletedRestyle"));

        var job = (await client.GetAsync(Id, Ct))!;

        job.Progress.ShouldBe(100);
        job.Takes.Count.ShouldBe(2);
        job.Takes[0].ShouldBe(new GeneratorTake(1, 1234567, 45, 48000, "9c4f2e7d1b3a5c6e8f0a1b2c3d4e5f60718293a4b5c6d7e8f9a0b1c2d3e4f5a6", 9012345, -0.9,
            new GeneratorTakeModel("acestep-v15-turbo", "19671f406d603126926c1b7e2adc169acbcade22")));
        job.Device.ShouldBe(new GeneratorJobDevice("gpu", "NVIDIA GeForce RTX 5070 Ti", false, null));
        job.Error.ShouldBeNull();
    }

    [Fact]
    public async Task Get_FailedCarriesTheError()
    {
        var (client, _) = Create(_ => GeneratorExamples.Response(HttpStatusCode.OK, "FailedOutOfMemory"));

        var job = (await client.GetAsync(Id, Ct))!;

        job.Error.ShouldBe(new GeneratorJobError("outOfMemory", "Not enough GPU memory for this request (try a shorter range or 1 take)."));
    }

    [Fact]
    public async Task Get_UnknownIsNull()
    {
        var (client, _) = Create(_ => GeneratorExamples.Problem(HttpStatusCode.NotFound, null, "Unknown"));

        (await client.GetAsync(Id, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Cancel_PostsAndParses()
    {
        var (client, handler) = Create(_ => GeneratorExamples.Response(HttpStatusCode.OK, "CancelledRestyle"));

        (await client.CancelAsync(Id, Ct))!.Status.ShouldBe(GeneratorJobStatus.Cancelled);

        (handler.Requests.Single().Method, handler.Requests.Single().Uri.AbsolutePath).ShouldBe((HttpMethod.Post, $"/v1/generations/{Id}/cancel"));
    }

    [Fact]
    public async Task Cancel_UnknownIsNull()
    {
        var (client, _) = Create(_ => GeneratorExamples.Problem(HttpStatusCode.NotFound, null, "Unknown"));

        (await client.CancelAsync(Id, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task DownloadTake_StreamsAndVerifiesTheHash()
    {
        var bytes = new byte[] { 5, 6, 7, 8 };
        var (client, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        await using var destination = new MemoryStream();

        await client.DownloadTakeAsync(Id, 2, destination, Sha(bytes), Ct);

        destination.ToArray().ShouldBe(bytes);
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/generations/{Id}/takes/2");
    }

    [Fact]
    public async Task DownloadTake_HashMismatchThrows()
    {
        var (client, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) });
        await using var destination = new MemoryStream();

        await Should.ThrowAsync<GeneratorIntegrityException>(() => client.DownloadTakeAsync(Id, 1, destination, new string('0', 64), Ct));
    }

    [Fact]
    public async Task DownloadTake_NotFoundIsARequestError()
    {
        var (client, _) = Create(_ => GeneratorExamples.Problem(HttpStatusCode.NotFound, null, "No take"));
        await using var destination = new MemoryStream();

        (await Should.ThrowAsync<GeneratorRequestException>(() => client.DownloadTakeAsync(Id, 1, destination, new string('0', 64), Ct))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Delete_IsIdempotent(HttpStatusCode status)
    {
        var (client, handler) = Create(_ => new HttpResponseMessage(status));

        await client.DeleteAsync(Id, Ct);

        (handler.Requests.Single().Method, handler.Requests.Single().Uri.AbsolutePath).ShouldBe((HttpMethod.Delete, $"/v1/generations/{Id}"));
    }

    [Fact]
    public async Task Delete_ServerErrorIsUnavailable()
    {
        var (client, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));

        await Should.ThrowAsync<GeneratorUnavailableException>(() => client.DeleteAsync(Id, Ct));
    }

    [Theory]
    [InlineData("../etc")]
    [InlineData("ABCDEF0123456789ABCDEF0123456789")]
    public async Task InvalidIds_AreRejectedWithoutARequest(string id)
    {
        var (client, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Should.ThrowAsync<ArgumentException>(() => client.GetAsync(id, Ct));
        await Should.ThrowAsync<ArgumentException>(() => client.DownloadTakeAsync(Id, 0, Stream.Null, "x", Ct));
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void Registration_BindsTheBaseUrl()
    {
        using var provider = Provider(new Dictionary<string, string?> { ["StemStudio:GeneratorBaseUrl"] = "http://generator:8010" });

        provider.GetRequiredService<IGeneratorClient>().IsConfigured.ShouldBeTrue();
        provider.GetRequiredService<IOptions<StemStudioApiOptions>>().Value.GeneratorBaseUrl.ShouldBe(new Uri("http://generator:8010"));
    }

    [Fact]
    public void Registration_WithoutBaseUrlIsNotConfigured()
    {
        using var provider = Provider([]);

        provider.GetRequiredService<IGeneratorClient>().IsConfigured.ShouldBeFalse();
    }

    [Fact]
    public async Task Registration_RetriesGetsButNeverPosts()
    {
        var calls = 0;
        var stub = new StubHttpMessageHandler(_ => Task.FromResult(++calls < 3 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : GeneratorExamples.Response(HttpStatusCode.OK, "QueuedRestyle")));
        using var provider = Provider(new Dictionary<string, string?> { ["StemStudio:GeneratorBaseUrl"] = "http://generator:8010" }, stub);
        var client = provider.GetRequiredService<IGeneratorClient>();

        (await client.GetAsync(Id, Ct))!.Status.ShouldBe(GeneratorJobStatus.Queued);
        calls = 0;
        await Should.ThrowAsync<GeneratorUnavailableException>(() => client.CancelAsync(Id, Ct));
        calls.ShouldBe(1);
    }

    private static GeneratorRestyleParams RangeParams() =>
        new("Jazz swing, swing feel, piano, upright bass, brushed drums, relaxed, warm", 0.55, 8, 3.0, [1234567, 7654321], RestyleMode.Range, 60, 105);

    private async Task<GeneratorStemFile> StemAsync(string name, byte[] bytes)
    {
        var path = Path.Combine(_tempDir, name + ".flac");
        await File.WriteAllBytesAsync(path, bytes, Ct);
        return new GeneratorStemFile(name, path);
    }

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    internal static (GeneratorClient Client, StubHttpMessageHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHttpMessageHandler(request => Task.FromResult(respond(request)));
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://generator:8010/") };
        return (new GeneratorClient(http, NullLogger<GeneratorClient>.Instance, TimeProvider.System), handler);
    }

    private static ServiceProvider Provider(Dictionary<string, string?> settings, HttpMessageHandler? stub = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddGeneratorClient(configuration);
        if (stub is not null)
        {
            builder.ConfigurePrimaryHttpMessageHandler(() => stub);
        }

        return services.BuildServiceProvider();
    }

    private sealed class HangingHandler : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}

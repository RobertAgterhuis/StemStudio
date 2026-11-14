using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StemStudio.Api.Sync;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Importing;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests;

/// <summary>T111: structured logs for job transitions, worker problems and imports; no stack traces reach clients.</summary>
public sealed class LoggingTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public LoggingTests() => _client = _factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Sync_LogsStatusTransitionsWithTrackIdAndReason()
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());
        _factory.Worker.OnGetJob = _ => Task.FromResult<WorkerJob?>(null);

        await SyncAsync();

        var entry = _factory.Logs.For(nameof(TrackSynchronizer)).Single(e => e.Level == LogLevel.Information);
        entry.Message.ShouldContain(track.Id.ToString());
        entry.Message.ShouldContain("Queued -> Failed");
        entry.Message.ShouldContain("interrupted by restart");
    }

    [Fact]
    public async Task Sync_UnchangedStatusIsNotLogged()
    {
        await _factory.SeedAsync(SyncTestData.UrlTrack());

        await SyncAsync();

        _factory.Logs.For(nameof(TrackSynchronizer)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_UnreachableWorkerIsAWarningWithoutException()
    {
        await _factory.SeedAsync(SyncTestData.UrlTrack());
        _factory.Worker.OnGetJob = _ => throw new WorkerUnavailableException();

        await SyncAsync();

        var entry = _factory.Logs.For(nameof(TrackSynchronizer)).Single();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Exception.ShouldBeNull();
    }

    [Fact]
    public async Task Sync_UnexpectedErrorIsLoggedWithException()
    {
        await _factory.SeedAsync(SyncTestData.UrlTrack());
        _factory.Worker.OnGetJob = _ => throw new InvalidOperationException("bug");

        await SyncAsync();

        _factory.Logs.For(nameof(TrackSynchronizer)).Single().Exception.ShouldBeOfType<InvalidOperationException>();
    }

    [Theory]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", 202)]
    [InlineData("https://vimeo.com/1", 400)]
    public async Task Import_LogsTheOutcome(string url, int status)
    {
        using var response = await _client.PostAsJsonAsync(new Uri(ApiRoutes.Imports, UriKind.Relative), new ImportUrlRequest(url), Ct);

        var entry = _factory.Logs.For("ImportEndpoints").Single();
        entry.Message.ShouldContain("url");
        entry.Message.ShouldContain(status.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Import_UploadOutcomeIsLogged()
    {
        using var content = Importing.ImportEndpointsTests.UploadContent([1], "a.ogg");

        using var response = await _client.PostAsync(new Uri(ApiRoutes.ImportUpload, UriKind.Relative), content, Ct);

        _factory.Logs.For("ImportEndpoints").Single().Message.ShouldContain("415");
    }

    [Fact]
    public async Task UnexpectedError_Returns500ProblemWithoutDetailsAndIsLogged()
    {
        _factory.Worker.OnCreateUrl = _ => throw new InvalidOperationException("secret internal state");

        using var response = await _client.PostAsJsonAsync(new Uri(ApiRoutes.Imports, UriKind.Relative), new ImportUrlRequest("https://youtu.be/dQw4w9WgXcQ"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadAsStringAsync(Ct);
        body.ShouldNotContain("secret");
        body.ShouldNotContain("   at ");
        _factory.Logs.Entries.ShouldContain(e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task WorkerClient_LogsTransportFailuresAndRejectionsWithoutStackTraces()
    {
        var logs = new ListLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        var calls = 0;
        var handler = new StubHttpMessageHandler(_ => ++calls == 1
            ? throw new HttpRequestException("Connection refused")
            : Task.FromResult(StubHttpMessageHandler.Example(HttpStatusCode.BadRequest, "problem-400")));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://worker:8000/") };
        var client = new WorkerClient(http, loggerFactory.CreateLogger<WorkerClient>());
        var request = new WorkerUrlJobRequest(SourceType.Youtube, "https://www.youtube.com/watch?v=dQw4w9WgXcQ");

        await Should.ThrowAsync<WorkerUnavailableException>(() => client.CreateUrlJobAsync(request, Ct));
        await Should.ThrowAsync<WorkerRequestException>(() => client.CreateUrlJobAsync(request, Ct));

        var entries = logs.For(nameof(WorkerClient)).ToList();
        entries.Count.ShouldBe(2);
        entries[0].Level.ShouldBe(LogLevel.Warning);
        entries[0].Message.ShouldContain("POST http://worker:8000/v1/jobs");
        entries[0].Message.ShouldContain("Connection refused");
        entries[0].Exception.ShouldBeNull();
        entries[1].Level.ShouldBe(LogLevel.Information);
        entries[1].Message.ShouldContain("400");
        entries[1].Message.ShouldContain("Only YouTube video and Spotify track URLs are supported");
    }

    private Task<bool> SyncAsync() => _factory.WithScopeAsync(async sp =>
    {
        await sp.GetRequiredService<ITrackSynchronizer>().SyncAllAsync(Ct);
        return true;
    });
}

using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Storage;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Tracks;

public sealed class StemEndpointTests : IDisposable
{
    private static readonly byte[] Flac = Enumerable.Range(0, 4096).Select(i => (byte)(i % 251)).ToArray();
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public StemEndpointTests() => _client = _factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetStem_ReturnsFlacWithCachingHeaders()
    {
        var track = await SeedCompletedWithFilesAsync();

        using var response = await SendAsync(track.Id, "guitar");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("audio/flac");
        response.Headers.AcceptRanges.ShouldContain("bytes");
        response.Headers.ETag.ShouldNotBeNull();
        response.Content.Headers.LastModified.ShouldNotBeNull();
        response.Content.Headers.ContentLength.ShouldBe(Flac.Length);
        (await response.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(Flac);
    }

    [Fact]
    public async Task GetStem_RangeReturns206WithContentRange()
    {
        var track = await SeedCompletedWithFilesAsync();

        using var response = await SendAsync(track.Id, "vocals", request => request.Headers.Range = new RangeHeaderValue(0, 1023));

        response.StatusCode.ShouldBe(HttpStatusCode.PartialContent);
        response.Content.Headers.ContentRange!.ToString().ShouldBe($"bytes 0-1023/{Flac.Length}");
        response.Headers.ETag.ShouldNotBeNull();
        response.Content.Headers.LastModified.ShouldNotBeNull();
        (await response.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(Flac[..1024]);
    }

    [Fact]
    public async Task GetStem_UnsatisfiableRangeReturns416WithSize()
    {
        var track = await SeedCompletedWithFilesAsync();

        using var response = await SendAsync(track.Id, "vocals", request => request.Headers.Range = new RangeHeaderValue(10_000, 20_000));

        response.StatusCode.ShouldBe(HttpStatusCode.RequestedRangeNotSatisfiable);
        response.Content.Headers.ContentRange!.ToString().ShouldBe($"bytes */{Flac.Length}");
    }

    [Fact]
    public async Task GetStem_IfNoneMatchReturns304WithValidators()
    {
        var track = await SeedCompletedWithFilesAsync();
        using var first = await SendAsync(track.Id, "bass");

        using var response = await SendAsync(track.Id, "bass", request => request.Headers.IfNoneMatch.Add(first.Headers.ETag!));

        response.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        response.Headers.ETag.ShouldBe(first.Headers.ETag);
        response.Content.Headers.LastModified.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetStem_IfModifiedSinceReturns304()
    {
        var track = await SeedCompletedWithFilesAsync();
        using var first = await SendAsync(track.Id, "bass");

        using var response = await SendAsync(track.Id, "bass", request => request.Headers.IfModifiedSince = first.Content.Headers.LastModified);

        response.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task GetStem_IfRangeHonoursTheEntityTag()
    {
        var track = await SeedCompletedWithFilesAsync();
        using var first = await SendAsync(track.Id, "drums");

        using var matching = await SendAsync(track.Id, "drums", request =>
        {
            request.Headers.Range = new RangeHeaderValue(0, 9);
            request.Headers.IfRange = new RangeConditionHeaderValue(first.Headers.ETag!);
        });
        using var stale = await SendAsync(track.Id, "drums", request =>
        {
            request.Headers.Range = new RangeHeaderValue(0, 9);
            request.Headers.IfRange = new RangeConditionHeaderValue(new EntityTagHeaderValue("\"stale\""));
        });

        matching.StatusCode.ShouldBe(HttpStatusCode.PartialContent);
        stale.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetStem_EntityTagChangesWhenTheFileChanges()
    {
        var track = await SeedCompletedWithFilesAsync();
        using var first = await SendAsync(track.Id, "other");
        var store = _factory.Services.GetRequiredService<IStemFileStore>();
        await store.SaveStemAsync(track.Id, "other", (stream, ct) => stream.WriteAsync(new byte[10], ct).AsTask(), Ct);
        File.SetLastWriteTimeUtc(store.GetStemPath(track.Id, "other"), DateTime.UtcNow.AddMinutes(5));

        using var second = await SendAsync(track.Id, "other");

        second.Headers.ETag.ShouldNotBe(first.Headers.ETag);
    }

    [Fact]
    public async Task GetStem_UnknownTrackReturns404Problem()
    {
        using var response = await SendAsync(Guid.NewGuid(), "vocals");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Theory]
    [InlineData("flute")]
    [InlineData("Vocals")]
    [InlineData("..%2Fstemstudio.db")]
    public async Task GetStem_UnknownStemNameReturns404(string stem)
    {
        var track = await SeedCompletedWithFilesAsync();

        using var response = await SendAsync(track.Id, stem);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetStem_StemNotOnTheTrackReturns404()
    {
        var track = SyncTestData.UrlTrack();
        track.Complete(SyncTestData.Completion() with { Model = "htdemucs", Stems = StemCatalog.FourStem.Select(n => new Stem(n, 1)).ToList() }, SyncTestData.T0);
        await _factory.SeedAsync(track);
        await WriteStemAsync(track.Id, "guitar");

        using var response = await SendAsync(track.Id, "guitar");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetStem_MissingFileReturns404()
    {
        var track = await _factory.SeedAsync(SyncTestData.CompletedTrack());

        using var response = await SendAsync(track.Id, "vocals");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetStem_TrackNotCompletedReturns404()
    {
        var track = await _factory.SeedAsync(SyncTestData.UrlTrack());
        await WriteStemAsync(track.Id, "vocals");

        using var response = await SendAsync(track.Id, "vocals");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("http://web.test", true)]
    [InlineData("http://evil.test", false)]
    public async Task GetStem_CorsAllowsOnlyTheWebOriginAndExposesRangeHeaders(string origin, bool allowed)
    {
        var track = await SeedCompletedWithFilesAsync();

        using var response = await SendAsync(track.Id, "vocals", request => CorsAssert.AddRangedOriginRequest(request, origin));

        CorsAssert.AllowsOnlyTheWebOrigin(response, allowed);
    }

    [Fact]
    public async Task GetStem_CorsPreflightAllowsRangeHeader()
    {
        var track = await SeedCompletedWithFilesAsync();
        using var request = new HttpRequestMessage(HttpMethod.Options, ApiRoutes.Stem(track.Id, "vocals"));
        request.Headers.Add("Origin", "http://web.test");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "range");

        using var response = await _client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers")).ToLowerInvariant().ShouldContain("range");
    }

    internal async Task<Track> SeedCompletedWithFilesAsync()
    {
        var track = await _factory.SeedAsync(SyncTestData.CompletedTrack());
        foreach (var stem in StemCatalog.SixStem)
        {
            await WriteStemAsync(track.Id, stem);
        }

        return track;
    }

    private Task<long> WriteStemAsync(Guid trackId, string stem) =>
        _factory.Services.GetRequiredService<IStemFileStore>().SaveStemAsync(trackId, stem, (s, ct) => s.WriteAsync(Flac, ct).AsTask(), Ct);

    private async Task<HttpResponseMessage> SendAsync(Guid trackId, string stem, Action<HttpRequestMessage>? configure = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/tracks/{trackId}/stems/{stem}");
        configure?.Invoke(request);
        return await _client.SendAsync(request, Ct);
    }
}

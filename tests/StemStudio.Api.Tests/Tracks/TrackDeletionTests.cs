using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StemStudio.Api.Storage;
using StemStudio.Api.Tests.Mixes;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Tracks;
using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Tracks;

/// <summary><c>DELETE /api/tracks/{trackId}</c> (contract 1.1.0).</summary>
public sealed class TrackDeletionTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public TrackDeletionTests() => _client = _factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Delete_CompletedTrack_RemovesRowMixesAndStemFilesWithoutCallingTheWorker()
    {
        var track = await SeedCompletedWithStemAsync("dQw4w9WgXcQ");
        var other = await SeedCompletedWithStemAsync("aaaaaaaaaaa");
        await CreateMixAsync(track.Id, "One");
        await CreateMixAsync(track.Id, "Two");
        var otherMix = await CreateMixAsync(other.Id, "One");

        using var response = await DeleteAsync(track.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _factory.FindTrackAsync(track.Id)).ShouldBeNull();
        (await ListMixesAsync()).Select(m => m.Id).ShouldBe([otherMix.Id]);
        Directory.Exists(Path.Combine(_factory.DataDir, "tracks", track.Id.ToString())).ShouldBeFalse();
        Stems.FindStem(other.Id, "vocals").ShouldNotBeNull();
        (_factory.Worker.CreateCalls + _factory.Worker.StemDownloads.Count).ShouldBe(0);
    }

    [Fact]
    public async Task Delete_FailedUploadTrack_RemovesTheStoredUpload()
    {
        var stored = await Uploads.SaveAsync(new MemoryStream([7, 7, 7]), ".wav", Ct);
        var track = await _factory.SeedAsync(FailedUpload(stored));

        using var response = await DeleteAsync(track.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Uploads.Exists(stored.FileName).ShouldBeFalse();
        (await _factory.FindTrackAsync(track.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task Delete_UploadTrackWhoseFileIsGone_Returns204()
    {
        var stored = await Uploads.SaveAsync(new MemoryStream([1]), ".mp3", Ct);
        var track = await _factory.SeedAsync(FailedUpload(stored));
        Uploads.Delete(stored.FileName);

        using var response = await DeleteAsync(track.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        _factory.Logs.For(nameof(TrackDeletionService)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(TrackStatus.Queued)]
    [InlineData(TrackStatus.Downloading)]
    [InlineData(TrackStatus.Separating)]
    [InlineData(TrackStatus.Analysing)]
    public async Task Delete_NonTerminalTrack_Returns409AndKeepsEverything(TrackStatus status)
    {
        var track = await _factory.SeedAsync(InStatus(status));
        await SaveStemAsync(track.Id);

        using var response = await DeleteAsync(track.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.JsonAsync();
        problem.GetProperty("title").GetString().ShouldBe("Track is still being processed");
        problem.GetProperty("detail").GetString().ShouldBe("The track is still being processed; wait until it has completed or failed.");
        (await _factory.FindTrackAsync(track.Id)).ShouldNotBeNull();
        Stems.FindStem(track.Id, "vocals").ShouldNotBeNull();
    }

    [Fact]
    public async Task Delete_UnknownTrack_Returns404Problem()
    {
        var id = Guid.NewGuid();

        using var response = await DeleteAsync(id);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.JsonAsync()).GetProperty("detail").GetString().ShouldBe($"Track {id} does not exist.");
    }

    [Theory]
    [InlineData(typeof(IOException), HttpStatusCode.NoContent, 1)]
    [InlineData(typeof(UnauthorizedAccessException), HttpStatusCode.NoContent, 1)]
    [InlineData(typeof(InvalidOperationException), HttpStatusCode.InternalServerError, 0)]
    public async Task Delete_FileCleanupFailure_KeepsTheRowDeletedAndLogsDiskErrorsAsWarnings(
        Type exceptionType, HttpStatusCode expected, int warnings)
    {
        var failing = new FailingStemFileStore((Exception)Activator.CreateInstance(exceptionType)!);
        using var factory = new ApiFactory { ConfigureServices = services => services.AddSingleton<IStemFileStore>(failing) };
        using var client = factory.CreateClient();
        var track = await factory.SeedAsync(SyncTestData.CompletedTrack());

        using var response = await client.DeleteAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(expected);
        (await factory.FindTrackAsync(track.Id)).ShouldBeNull();
        var logged = factory.Logs.For(nameof(TrackDeletionService)).ToList();
        logged.Count.ShouldBe(warnings);
        logged.ShouldAllBe(entry => entry.Level == LogLevel.Warning && entry.Message.Contains(track.Id.ToString()) && entry.Exception!.GetType() == exceptionType);
    }

    private IStemFileStore Stems => _factory.Services.GetRequiredService<IStemFileStore>();

    private IUploadStore Uploads => _factory.Services.GetRequiredService<IUploadStore>();

    private Task<HttpResponseMessage> DeleteAsync(Guid id) =>
        _client.DeleteAsync(new Uri(ApiRoutes.Track(id), UriKind.Relative), Ct);

    private Task<long> SaveStemAsync(Guid trackId) =>
        Stems.SaveStemAsync(trackId, "vocals", (s, ct) => s.WriteAsync(new byte[16], ct).AsTask(), Ct);

    private async Task<Track> SeedCompletedWithStemAsync(string videoId)
    {
        var track = await _factory.SeedAsync(SyncTestData.CompletedTrack(videoId));
        await SaveStemAsync(track.Id);
        return track;
    }

    private async Task<MixDto> CreateMixAsync(Guid trackId, string name)
    {
        using var response = await _client.PostAsJsonAsync(
            new Uri(ApiRoutes.Mixes, UriKind.Relative), new CreateMixRequest(trackId, MixEndpointsTests.Settings(name)), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.BodyAsync<MixDto>();
    }

    private async Task<List<MixSummaryDto>> ListMixesAsync()
    {
        using var response = await _client.GetAsync(new Uri(ApiRoutes.Mixes, UriKind.Relative), Ct);
        return await response.BodyAsync<List<MixSummaryDto>>();
    }

    private static Track InStatus(TrackStatus status)
    {
        var track = SyncTestData.UrlTrack();
        if (status != TrackStatus.Queued)
        {
            track.ApplyProgress(new TrackProgress(status, "Working", 40), SyncTestData.T0);
        }

        return track;
    }

    private static Track FailedUpload(StoredUpload stored)
    {
        var track = Track.FromUpload(Guid.NewGuid(), SourceKey.ForUpload(stored.Sha256), new UploadedFile("a" + Path.GetExtension(stored.FileName), stored.FileName), SyncTestData.T0);
        track.Fail("Separation failed.", SyncTestData.T0);
        return track;
    }

    private sealed class FailingStemFileStore(Exception failure) : IStemFileStore
    {
        public string GetStemPath(Guid trackId, string stem) => throw new NotSupportedException();

        public FileInfo? FindStem(Guid trackId, string stem) => throw new NotSupportedException();

        public Task<long> SaveStemAsync(Guid trackId, string stem, Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void DeleteTrackFiles(Guid trackId) => throw failure;
    }
}

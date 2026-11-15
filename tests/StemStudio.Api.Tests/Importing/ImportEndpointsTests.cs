using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StemStudio.Api.Importing;
using StemStudio.Api.Storage;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Importing;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Importing;

public sealed class ImportEndpointsTests : IDisposable
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ImportEndpointsTests()
        : this(new ApiFactory())
    {
    }

    private ImportEndpointsTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task ImportUrl_NewSource_Returns202QueuedTrackAndStartsWorkerJobWithCanonicalUrl()
    {
        using var response = await PostUrlAsync("https://youtu.be/dQw4w9WgXcQ?si=abc");

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var track = await response.BodyAsync<TrackDto>();
        response.Headers.Location!.ToString().ShouldBe(ApiRoutes.Track(track.Id));
        track.Status.ShouldBe(TrackStatusDto.Queued);
        track.Title.ShouldBe("https://youtu.be/dQw4w9WgXcQ?si=abc");
        track.SourceUrl.ShouldBe("https://youtu.be/dQw4w9WgXcQ?si=abc");
        track.SourceKey.ShouldBe("youtube:dQw4w9WgXcQ");
        track.SourceType.ShouldBe(SourceTypeDto.Youtube);
        track.Stems.ShouldBeEmpty();
        _factory.Worker.UrlRequests.ShouldBe([new WorkerUrlJobRequest(SourceType.Youtube, "https://www.youtube.com/watch?v=dQw4w9WgXcQ")]);
        (await _factory.FindTrackAsync(track.Id))!.WorkerJobId.ShouldNotBeNull();
    }

    [Fact]
    public async Task ImportUrl_Spotify_ForwardsCanonicalTrackUrl()
    {
        using var response = await PostUrlAsync("https://open.spotify.com/intl-nl/track/4cOdK2wGLETKBW3PvgPWqT?si=x");

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _factory.Worker.UrlRequests.Single().ShouldBe(new WorkerUrlJobRequest(SourceType.Spotify, "https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT"));
    }

    [Fact]
    public async Task ImportUrl_ExistingSource_Returns200SameTrackWithoutNewJob()
    {
        using var first = await PostUrlAsync("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        var created = await first.BodyAsync<TrackDto>();

        using var second = await PostUrlAsync("https://youtu.be/dQw4w9WgXcQ");

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await second.BodyAsync<TrackDto>()).Id.ShouldBe(created.Id);
        _factory.Worker.CreateCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData("https://www.youtube.com/playlist?list=PLx0sYbCqOb8TBPRdmBHs5Iftvv9TPboYG", "Playlists")]
    [InlineData("https://open.spotify.com/album/4aawyAB9vmqN3uQ7FjRGTy", "albums")]
    [InlineData("https://vimeo.com/76979871", "Only YouTube")]
    [InlineData("", "Enter")]
    public async Task ImportUrl_InvalidUrl_Returns400WithActionableMessageAndPersistsNothing(string url, string message)
    {
        using var response = await PostUrlAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        (await response.FieldErrorsAsync("url")).Single().ShouldContain(message);
        (await _factory.AllTracksAsync()).ShouldBeEmpty();
        _factory.Worker.CreateCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ImportUrl_EmptyBody_Returns400OnUrl()
    {
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        using var response = await _client.PostAsync(new Uri(ApiRoutes.Imports, UriKind.Relative), content, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync("url")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ImportUrl_MalformedJson_Returns400ValidationProblem()
    {
        using var content = new StringContent("{ not json", Encoding.UTF8, "application/json");

        using var response = await _client.PostAsync(new Uri(ApiRoutes.Imports, UriKind.Relative), content, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var json = await response.JsonAsync();
        json.GetProperty("errors").EnumerateObject().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ImportUrl_WorkerUnavailable_Returns503AndPersistsNothing()
    {
        _factory.Worker.OnCreateUrl = _ => throw new WorkerUnavailableException();

        using var response = await PostUrlAsync("https://youtu.be/dQw4w9WgXcQ");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var problem = await response.JsonAsync();
        problem.GetProperty("title").GetString().ShouldBe("Processing service unavailable");
        problem.GetProperty("detail").GetString()!.ShouldContain("Existing tracks and mixes remain usable");
        (await _factory.AllTracksAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ImportUrl_Worker400_Returns400OnUrlAndPersistsNothing()
    {
        _factory.Worker.OnCreateUrl = _ => throw new WorkerRequestException("This video is not available.");

        using var response = await PostUrlAsync("https://youtu.be/dQw4w9WgXcQ");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync("url")).ShouldBe(["This video is not available."]);
        (await _factory.AllTracksAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ImportUrl_ParallelImportsOfSameSource_OneWorkerCallSameTrack()
    {
        var workerCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _factory.Worker.OnCreateUrl = async _ =>
        {
            workerCalled.SetResult();
            await release.Task;
            return FakeWorkerClient.Job(_factory.Worker.NextJobId(), WorkerJobState.Queued);
        };

        var first = PostUrlAsync("https://youtu.be/dQw4w9WgXcQ");
        await workerCalled.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        using var second = await PostUrlAsync("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=3s");
        release.SetResult();
        using var firstResponse = await first;

        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await second.BodyAsync<TrackDto>()).Id.ShouldBe((await firstResponse.BodyAsync<TrackDto>()).Id);
        _factory.Worker.CreateCalls.ShouldBe(1);
    }

    [Fact]
    public async Task ImportUpload_NewFile_Returns202StoresFileAndStreamsItToWorker()
    {
        var bytes = Encoding.UTF8.GetBytes("ID3 fake mp3 content");
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));

        using var response = await PostUploadAsync(bytes, "My Song.MP3");

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var track = await response.BodyAsync<TrackDto>();
        response.Headers.Location!.ToString().ShouldBe(ApiRoutes.Track(track.Id));
        track.Title.ShouldBe("My Song");
        track.SourceKey.ShouldBe("upload:" + sha);
        track.SourceType.ShouldBe(SourceTypeDto.Upload);
        track.SourceUrl.ShouldBeNull();
        var stored = Path.Combine(_factory.DataDir, "uploads", sha + ".mp3");
        (await File.ReadAllBytesAsync(stored, Ct)).ShouldBe(bytes);
        var upload = _factory.Worker.Uploads.Single();
        upload.Upload.FileName.ShouldBe(sha + ".mp3");
        upload.Bytes.ShouldBe(bytes);
    }

    [Fact]
    public async Task ImportUpload_SameFileAgain_Returns200SameTrack()
    {
        using var first = await PostUploadAsync([1, 2, 3], "a.wav");
        using var second = await PostUploadAsync([1, 2, 3], "renamed.wav");

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await second.BodyAsync<TrackDto>()).Id.ShouldBe((await first.BodyAsync<TrackDto>()).Id);
        _factory.Worker.CreateCalls.ShouldBe(1);
        Directory.GetFiles(Path.Combine(_factory.DataDir, "uploads")).Length.ShouldBe(1);
    }

    [Fact]
    public async Task ImportUpload_SameFileForCompletedTrack_DiscardsTheNewCopy()
    {
        using var first = await PostUploadAsync([4, 5, 6], "a.flac");
        var track = await first.BodyAsync<TrackDto>();
        await _factory.UpdateTrackAsync(track.Id, t => t.Complete(SyncTestData.Completion(), DateTime.UtcNow));
        Directory.Delete(Path.Combine(_factory.DataDir, "uploads"), true);

        using var second = await PostUploadAsync([4, 5, 6], "a.flac");

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        Directory.GetFiles(Path.Combine(_factory.DataDir, "uploads")).ShouldBeEmpty();
    }

    [Fact]
    public async Task ImportUpload_SameBytesOtherExtension_DiscardsTheNewCopy()
    {
        using var first = await PostUploadAsync([7, 7], "a.mp3");

        using var second = await PostUploadAsync([7, 7], "a.wav");

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        Directory.GetFiles(Path.Combine(_factory.DataDir, "uploads")).Select(Path.GetExtension).ShouldBe([".mp3"]);
    }

    [Theory]
    [InlineData("song.ogg")]
    [InlineData("song")]
    [InlineData("song.mp3.exe")]
    public async Task ImportUpload_UnsupportedType_Returns415AndStoresNothing(string fileName)
    {
        using var response = await PostUploadAsync([1], fileName);

        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        (await response.JsonAsync()).GetProperty("title").GetString().ShouldBe("Unsupported file type");
        Directory.Exists(Path.Combine(_factory.DataDir, "uploads")).ShouldBeFalse();
        _factory.Worker.CreateCalls.ShouldBe(0);
    }

    [Fact]
    public async Task ImportUpload_NotMultipart_Returns400OnFile()
    {
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");

        using var response = await _client.PostAsync(new Uri(ApiRoutes.ImportUpload, UriKind.Relative), content, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync("file")).Single().ShouldContain("multipart/form-data");
    }

    [Fact]
    public async Task ImportUpload_WithoutFilePart_Returns400OnFile()
    {
        using var content = new MultipartFormDataContent { { new StringContent("x"), "other" } };

        using var response = await _client.PostAsync(new Uri(ApiRoutes.ImportUpload, UriKind.Relative), content, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync("file")).Single().ShouldContain("file");
    }

    [Fact]
    public async Task ImportUpload_EmptyFile_Returns400OnFile()
    {
        using var response = await PostUploadAsync([], "empty.mp3");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync("file")).Single().ShouldContain("empty");
    }

    [Fact]
    public async Task ImportUpload_WorkerUnavailable_Returns503AndDeletesUpload()
    {
        _factory.Worker.OnCreateUpload = _ => throw new WorkerUnavailableException();

        using var response = await PostUploadAsync([1, 2], "a.mp3");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await _factory.AllTracksAsync()).ShouldBeEmpty();
        Directory.GetFiles(Path.Combine(_factory.DataDir, "uploads")).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.RequestEntityTooLarge)]
    [InlineData(HttpStatusCode.UnsupportedMediaType, HttpStatusCode.UnsupportedMediaType)]
    public async Task ImportUpload_WorkerRejects_MapsStatusAndDeletesUpload(HttpStatusCode workerStatus, HttpStatusCode expected)
    {
        _factory.Worker.OnCreateUpload = _ => throw new WorkerRequestException(workerStatus, "Rejected by worker.");

        using var response = await PostUploadAsync([1, 2], "a.mp3");

        response.StatusCode.ShouldBe(expected);
        (await _factory.AllTracksAsync()).ShouldBeEmpty();
        Directory.GetFiles(Path.Combine(_factory.DataDir, "uploads")).ShouldBeEmpty();
    }

    [Fact]
    public async Task ImportUpload_LargerThanLimit_Returns413AndStoresNothing()
    {
        using var factory = new ApiFactory
        {
            ConfigureServices = services => services.AddSingleton<IUploadStore>(sp => new UploadStore(sp.GetRequiredService<DataDirectory>(), maxBytes: 10)),
        };
        using var client = factory.CreateClient();
        using var content = UploadContent(new byte[11], "big.wav");

        using var response = await client.PostAsync(new Uri(ApiRoutes.ImportUpload, UriKind.Relative), content, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await response.JsonAsync()).GetProperty("detail").GetString().ShouldBe("The file is larger than 200 MiB (209,715,200 bytes).");
        Directory.GetFiles(Path.Combine(factory.DataDir, "uploads")).ShouldBeEmpty();
        factory.Worker.CreateCalls.ShouldBe(0);
    }

    [Fact]
    public void RequestLimits_AllowExactly200MiBPlusMultipartOverhead()
    {
        var kestrel = _factory.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;
        var form = _factory.Services.GetRequiredService<IOptions<FormOptions>>().Value;

        kestrel.Limits.MaxRequestBodySize.ShouldBe(ContractLimits.MaxUploadBytes + UploadLimits.MultipartOverheadBytes);
        form.MultipartBodyLengthLimit.ShouldBe(ContractLimits.MaxUploadBytes + UploadLimits.MultipartOverheadBytes);
        UploadLimits.MultipartOverheadBytes.ShouldBeInRange(64 * 1024, 4 * 1024 * 1024);
    }

    private Task<HttpResponseMessage> PostUrlAsync(string url) =>
        _client.PostAsJsonAsync(new Uri(ApiRoutes.Imports, UriKind.Relative), new ImportUrlRequest(url), Ct);

    private async Task<HttpResponseMessage> PostUploadAsync(byte[] bytes, string fileName)
    {
        using var content = UploadContent(bytes, fileName);
        return await _client.PostAsync(new Uri(ApiRoutes.ImportUpload, UriKind.Relative), content, Ct);
    }

    internal static MultipartFormDataContent UploadContent(byte[] bytes, string fileName)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent { { file, "file", fileName } };
    }
}

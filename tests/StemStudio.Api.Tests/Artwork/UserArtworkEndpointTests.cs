using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Artwork;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Artwork;

/// <summary>Feature 005 T046: <c>PUT</c> and <c>DELETE /api/tracks/{id}/artwork</c> (FR-013).</summary>
public sealed class UserArtworkEndpointTests : ApiTestBase
{
    private const string ArtworkId = "8e4f2a6c0b1d4e3f9a7c5b3d1e0f2a4c";
    private static readonly string UserHash = new('e', 64);
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

    [Fact]
    public async Task Put_ForwardsThePictureAndShowsTheNormalisedResult()
    {
        var track = await SeedWithAutoArtworkAsync();

        using var response = await PutAsync(track.Id, Png, "me.png", "image/png");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "setTrackArtwork");
        var dto = await response.BodyAsync<TrackDto>();
        (dto.Artwork!.Source, dto.Artwork.Version).ShouldBe((ArtworkSourceDto.User, "eeeeeeeeeeee"));
        var upload = Worker.ArtworkUploads.Single();
        (upload.FileName, upload.ContentType, upload.Bytes).ShouldBe(("me.png", "image/png", Png));
        var cover = new WorkerArtworkRef(WorkerArtworkOwner.Upload, ArtworkId, ApiRoutes.ArtworkCover);
        Worker.ArtworkDownloads.ShouldBe([cover, cover with { Variant = ApiRoutes.ArtworkThumb }], ignoreOrder: true);
        (await GetCoverAsync(track.Id)).ShouldBe(FakeWorkerClient.ArtworkBytes(cover));
        (await Factory.FindTrackAsync(track.Id))!.AutoArtwork!.Source.ShouldBe(ArtworkSource.Youtube);
    }

    [Fact]
    public async Task Put_MoreThan10MiBIs413WithoutCallingTheWorker()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        using var response = await PutAsync(track.Id, new byte[ContractLimits.MaxArtworkUploadBytes + 1], "big.jpg", "image/jpeg");

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        await ContractAssert.MatchesAsync(response, "setTrackArtwork");
        Worker.ArtworkUploads.ShouldBeEmpty();
    }

    [Fact]
    public async Task Put_Exactly10MiBIsForwarded()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        using var response = await PutAsync(track.Id, new byte[ContractLimits.MaxArtworkUploadBytes], "edge.jpg", "image/jpeg");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Worker.ArtworkUploads.Single().Bytes.Length.ShouldBe((int)ContractLimits.MaxArtworkUploadBytes);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "Not an image")]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, "Picture too large")]
    [InlineData(HttpStatusCode.UnsupportedMediaType, "Unsupported picture type")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "Picture too large")]
    public async Task Put_WorkerRejectionsKeepTheirStatus(HttpStatusCode status, string title)
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        Worker.OnNormalizeArtwork = _ => throw new WorkerRequestException(status, "The worker says no.");

        using var response = await PutAsync(track.Id, Png, "me.png", "image/png");

        response.StatusCode.ShouldBe(status);
        await ContractAssert.MatchesAsync(response, "setTrackArtwork");
        var problem = await response.JsonAsync();
        (problem.GetProperty("title").GetString(), problem.GetProperty("detail").GetString()).ShouldBe((title, "The worker says no."));
        (await Factory.FindTrackAsync(track.Id))!.Artwork.ShouldBeNull();
    }

    [Fact]
    public async Task Put_AnUnexpectedWorkerRejectionIsABadPicture()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        Worker.OnNormalizeArtwork = _ => throw new WorkerRequestException(HttpStatusCode.NotFound, "Gone.");

        using var response = await PutAsync(track.Id, Png, "me.png", "image/png");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_UnreachableWorkerIs503()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        Worker.OnNormalizeArtwork = _ => throw new WorkerUnavailableException();

        using var response = await PutAsync(track.Id, Png, "me.png", "image/png");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        await ContractAssert.MatchesAsync(response, "setTrackArtwork");
    }

    [Fact]
    public async Task Put_NormalisedFilesThatVanishedAre503()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        Worker.OnGetArtwork = _ => Task.FromResult<byte[]?>(null);

        using var response = await PutAsync(track.Id, Png, "me.png", "image/png");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await Factory.FindTrackAsync(track.Id))!.Artwork.ShouldBeNull();
    }

    [Fact]
    public async Task Put_AnInvalidWorkerHashIs503()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        Worker.OnNormalizeArtwork = _ => Task.FromResult(new WorkerArtworkUpload(ArtworkId, new WorkerArtworkInfo(WorkerArtworkSource.User, "bad", 1, 1)));

        using var response = await PutAsync(track.Id, Png, "me.png", "image/png");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Put_UnknownTrackIs404()
    {
        using var response = await PutAsync(Guid.NewGuid(), Png, "me.png", "image/png");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(response, "setTrackArtwork");
        Worker.ArtworkUploads.ShouldBeEmpty();
    }

    [Fact]
    public async Task Put_WithoutAFilePartIs400()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        using var json = new StringContent("{}", Encoding.UTF8, "application/json");
        using var form = new MultipartFormDataContent { { new StringContent("x"), "other" } };

        using var notMultipart = await Client.PutAsync(new Uri(ApiRoutes.TrackArtworkUpload(track.Id), UriKind.Relative), json, Ct);
        using var noFile = await Client.PutAsync(new Uri(ApiRoutes.TrackArtworkUpload(track.Id), UriKind.Relative), form, Ct);

        notMultipart.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        noFile.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ContractAssert.MatchesAsync(noFile, "setTrackArtwork");
    }

    [Fact]
    public async Task Delete_ReturnsToTheAutomaticPictureAndRemovesTheUserFiles()
    {
        var track = await SeedWithAutoArtworkAsync();
        (await PutAsync(track.Id, Png, "me.png", "image/png")).Dispose();
        var userFolder = Path.Combine(Factory.DataDir, "tracks", track.Id.ToString(), "artwork", "user");
        Directory.Exists(userFolder).ShouldBeTrue();

        using var response = await DeleteAsync(track.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "resetTrackArtwork");
        (await response.BodyAsync<TrackDto>()).Artwork!.Source.ShouldBe(ArtworkSourceDto.Youtube);
        Directory.Exists(userFolder).ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_WithoutAUserPictureIsUnchanged()
    {
        var track = await SeedWithAutoArtworkAsync();

        using var response = await DeleteAsync(track.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Factory.FindTrackAsync(track.Id))!.UpdatedAt.ShouldBe(track.UpdatedAt);
    }

    [Fact]
    public async Task Delete_UnknownTrackIs404()
    {
        using var response = await DeleteAsync(Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(response, "resetTrackArtwork");
    }

    private async Task<Track> SeedWithAutoArtworkAsync()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await Factory.Services.GetRequiredService<IArtworkFileStore>().SaveAsync(track.Id, ArtworkSlot.Auto, new ArtworkImages([1], [2]), Ct);
        return await Factory.UpdateTrackAsync(track.Id, t => t.OfferArtwork(new ArtworkCandidate(ArtworkSource.Youtube, ArtworkSyncTests.HashA), SyncTestData.T0));
    }

    private async Task<byte[]> GetCoverAsync(Guid trackId) =>
        await (await Client.GetAsync(new Uri(ApiRoutes.TrackArtwork(trackId, ApiRoutes.ArtworkCover), UriKind.Relative), Ct)).Content.ReadAsByteArrayAsync(Ct);

    private async Task<HttpResponseMessage> PutAsync(Guid trackId, byte[] bytes, string fileName, string mediaType)
    {
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        using var content = new MultipartFormDataContent { { part, "file", fileName } };
        return await Client.PutAsync(new Uri(ApiRoutes.TrackArtworkUpload(trackId), UriKind.Relative), content, Ct);
    }

    private Task<HttpResponseMessage> DeleteAsync(Guid trackId) =>
        Client.DeleteAsync(new Uri(ApiRoutes.TrackArtworkUpload(trackId), UriKind.Relative), Ct);
}

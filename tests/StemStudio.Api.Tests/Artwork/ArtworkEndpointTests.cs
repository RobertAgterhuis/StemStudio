using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Artwork;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Artwork;

/// <summary>Feature 005 T026: <c>GET /api/tracks/{id}/artwork/{variant}</c> and <c>TrackDto.artwork</c> (FR-006, FR-007, FR-016).</summary>
public sealed class ArtworkEndpointTests : ApiTestBase
{
    private static readonly string Hash = ArtworkSyncTests.HashA;
    private static readonly byte[] Cover = [0xFF, 0xD8, 1, 2, 3, 0xFF, 0xD9];
    private static readonly byte[] Thumb = [0xFF, 0xD8, 4, 0xFF, 0xD9];

    [Fact]
    public async Task GetCover_WithTheCurrentVersionIsImmutable()
    {
        var track = await SeedWithArtworkAsync();

        using var response = await GetAsync(ApiRoutes.TrackArtwork(track.Id, ApiRoutes.ArtworkCover, "9b74c9897bac"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("image/jpeg");
        (await response.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(Cover);
        response.Headers.ETag!.Tag.ShouldBe("\"9b74c9897bac\"");
        response.Headers.CacheControl!.ToString().ShouldBe("private, max-age=31536000, immutable");
        await ContractAssert.MatchesAsync(response, "getTrackArtwork");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("000000000000")]
    public async Task GetThumb_WithoutOrWithAStaleVersionIsNoCache(string? version)
    {
        var track = await SeedWithArtworkAsync();

        using var response = await GetAsync(ApiRoutes.TrackArtwork(track.Id, ApiRoutes.ArtworkThumb, version));

        (await response.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(Thumb);
        response.Headers.CacheControl!.NoCache.ShouldBeTrue();
    }

    [Fact]
    public async Task IfNoneMatch_WithTheVersionIs304WithTheETag()
    {
        var track = await SeedWithArtworkAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.TrackArtwork(track.Id, ApiRoutes.ArtworkCover));
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"9b74c9897bac\""));

        using var response = await Client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        response.Headers.ETag!.Tag.ShouldBe("\"9b74c9897bac\"");
        await ContractAssert.MatchesAsync(response, "getTrackArtwork");
    }

    [Fact]
    public async Task NoArtwork_UnknownTrackAndUnknownVariant_Are404()
    {
        var bare = await Factory.SeedAsync(SyncTestData.CompletedTrack("aaaaaaaaaaa"));
        var withArtwork = await SeedWithArtworkAsync();

        using var none = await GetAsync(ApiRoutes.TrackArtwork(bare.Id, ApiRoutes.ArtworkCover));
        using var unknown = await GetAsync(ApiRoutes.TrackArtwork(Guid.NewGuid(), ApiRoutes.ArtworkCover));
        using var variant = await GetAsync(ApiRoutes.TrackArtwork(withArtwork.Id, "large"));

        foreach (var response in new[] { none, unknown, variant })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        await ContractAssert.MatchesAsync(none, "getTrackArtwork");
        await ContractAssert.MatchesAsync(unknown, "getTrackArtwork");
    }

    [Fact]
    public async Task MissingFile_Is404()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await Factory.UpdateTrackAsync(track.Id, t => t.OfferArtwork(new ArtworkCandidate(ArtworkSource.Youtube, Hash), SyncTestData.T0));

        using var response = await GetAsync(ApiRoutes.TrackArtwork(track.Id, ApiRoutes.ArtworkCover));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AUserPicture_IsServedFromItsOwnFolder()
    {
        var track = await SeedWithArtworkAsync();
        byte[] own = [0xFF, 0xD8, 9, 0xFF, 0xD9];
        await Store.SaveAsync(track.Id, ArtworkSlot.User, new ArtworkImages(own, own), Ct);
        await Factory.UpdateTrackAsync(track.Id, t => t.SetUserArtwork(new string('d', 64), SyncTestData.T0));

        using var response = await GetAsync(ApiRoutes.TrackArtwork(track.Id, ApiRoutes.ArtworkCover));

        (await response.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(own);
        response.Headers.ETag!.Tag.ShouldBe("\"dddddddddddd\"");
    }

    [Fact]
    public async Task TrackDto_CarriesArtworkUrlsWithTheVersionAndTheMetadataState()
    {
        var track = await SeedWithArtworkAsync();
        var bare = await Factory.SeedAsync(SyncTestData.CompletedTrack("aaaaaaaaaaa"));

        var dto = await (await GetAsync(ApiRoutes.Track(track.Id))).BodyAsync<TrackDto>();
        var bareDto = await (await GetAsync(ApiRoutes.Track(bare.Id))).BodyAsync<TrackDto>();

        dto.Artwork.ShouldBe(new ArtworkDto(
            ArtworkSourceDto.Youtube,
            "9b74c9897bac",
            $"/api/tracks/{track.Id}/artwork/cover?v=9b74c9897bac",
            $"/api/tracks/{track.Id}/artwork/thumb?v=9b74c9897bac"));
        bareDto.Artwork.ShouldBeNull();
        bareDto.Metadata.Status.ShouldNotBe(MetadataStatusDto.Searching);
    }

    [Fact]
    public async Task DeletingATrack_DeletesItsPictures()
    {
        var track = await SeedWithArtworkAsync();
        var folder = Path.Combine(Factory.DataDir, "tracks", track.Id.ToString(), "artwork");
        Directory.Exists(folder).ShouldBeTrue();

        using var response = await Client.DeleteAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Directory.Exists(folder).ShouldBeFalse();
    }

    private IArtworkFileStore Store => Factory.Services.GetRequiredService<IArtworkFileStore>();

    private async Task<Track> SeedWithArtworkAsync()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await Store.SaveAsync(track.Id, ArtworkSlot.Auto, new ArtworkImages(Cover, Thumb), Ct);
        return await Factory.UpdateTrackAsync(track.Id, t => t.OfferArtwork(new ArtworkCandidate(ArtworkSource.Youtube, Hash), SyncTestData.T0));
    }

    private Task<HttpResponseMessage> GetAsync(string path) => Client.GetAsync(new Uri(path, UriKind.Relative), Ct);
}

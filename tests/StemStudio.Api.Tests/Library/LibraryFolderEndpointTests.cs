using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Library;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Library;
using static StemStudio.Api.Tests.Library.LibraryTestData;

namespace StemStudio.Api.Tests.Library;

/// <summary>022 T042: <c>GET /api/library/folders[/{folderId}]</c> (backend-api 1.22.0, R-06).</summary>
public sealed class LibraryFolderEndpointTests : ApiTestBase
{
    private Task<HttpResponseMessage> GetAsync(string url, string? ifNoneMatch = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Relative));
        if (ifNoneMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
        }

        return Client.SendAsync(request, Ct);
    }

    private async Task<Asset> SeedItemAsync()
    {
        var track = await Factory.SeedAsync(MatchedTrack());
        var item = Asset.Create(Guid.NewGuid(), "Enter Sandman - drums", ExportProvenance(track.Id), T0);
        await Factory.FileAsync(item);
        return item;
    }

    [Fact]
    public async Task Root_ListsArtistsWithTheLibraryETag()
    {
        await SeedItemAsync();

        using var response = await GetAsync(ApiRoutes.LibraryFolders);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "getLibraryRoot");
        var view = await response.BodyAsync<LibraryFolderViewDto>();
        (view.Children.Single().Name, view.TotalItems, view.PendingFiling).ShouldBe(("Metallica", 1, 0));
        response.Headers.ETag!.ToString().ShouldBe(ApiRoutes.LibraryETag(view.Revision));
        view.Revision.ShouldMatch(ContractLimits.LibraryRevisionPattern);
    }

    [Fact]
    public async Task SameRevision_Is304_AndAChangeGivesANewOne()
    {
        var item = await SeedItemAsync();
        using var first = await GetAsync(ApiRoutes.LibraryFolders);
        var etag = first.Headers.ETag!.ToString();

        using var unchanged = await GetAsync(ApiRoutes.LibraryFolders, etag);
        using var other = await GetAsync(ApiRoutes.LibraryFolders, "\"lib-0000000000000000\", " + etag);
        (await Client.DeleteAsync(new Uri(ApiRoutes.Asset(item.Id), UriKind.Relative), Ct)).Dispose();
        using var changed = await GetAsync(ApiRoutes.LibraryFolders, etag);

        (unchanged.StatusCode, other.StatusCode, changed.StatusCode).ShouldBe((HttpStatusCode.NotModified, HttpStatusCode.NotModified, HttpStatusCode.OK));
        unchanged.Headers.ETag!.ToString().ShouldBe(etag);
        changed.Headers.ETag!.ToString().ShouldNotBe(etag);
    }

    [Fact]
    public async Task EveryLevel_MatchesTheContract()
    {
        var item = await SeedItemAsync();
        var placement = item.Placement!;

        foreach (var id in new[] { placement.ArtistFolderId, placement.AlbumFolderId, placement.SongFolderId })
        {
            using var response = await GetAsync(ApiRoutes.LibraryFolder(id));
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            await ContractAssert.MatchesAsync(response, "getLibraryFolder");
            (await response.BodyAsync<LibraryFolderViewDto>()).Folder!.Id.ShouldBe(id);
        }

        using var song = await GetAsync(ApiRoutes.LibraryFolder(placement.SongFolderId));
        var view = await song.BodyAsync<LibraryFolderViewDto>();
        (view.Items.Single().Id, view.Breadcrumb.Count, view.Folder!.TrackIds.Count).ShouldBe((item.Id, 2, 1));
    }

    [Fact]
    public async Task UnknownOrEmptiedFolder_Is404()
    {
        var item = await SeedItemAsync();
        (await Client.DeleteAsync(new Uri(ApiRoutes.Asset(item.Id), UriKind.Relative), Ct)).Dispose();

        using var emptied = await GetAsync(ApiRoutes.LibraryFolder(item.Placement!.SongFolderId));
        using var unknown = await GetAsync(ApiRoutes.LibraryFolder(FolderId.Artist("nobody")));

        foreach (var response in new[] { emptied, unknown })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            await ContractAssert.MatchesAsync(response, "getLibraryFolder");
            (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe(LibraryProblemTypes.Uri(LibraryProblemTypes.FolderNotFound));
        }
    }

    [Theory]
    [InlineData("/api/library/folders/xx0123456789abcdef", "folderId")]
    [InlineData("/api/library/folders/AR0123456789ABCDEF", "folderId")]
    [InlineData("/api/library/folders?sort=year", "sort")]
    [InlineData("/api/library/folders/al0123456789abcdef?sort=year", "sort")]
    [InlineData("/api/library/folders?sort=shuffle", "sort")]
    [InlineData("/api/library/folders?kind=video", "kind")]
    [InlineData("/api/library/folders?status=0", "status")]
    [InlineData("/api/library/folders?limit=0", "limit")]
    [InlineData("/api/library/folders?limit=501", "limit")]
    [InlineData("/api/library/folders?offset=-1", "offset")]
    public async Task InvalidQueries_Are422(string url, string field)
    {
        using var response = await GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe(LibraryProblemTypes.Uri(LibraryProblemTypes.LibraryQueryInvalid));
        (await response.FieldErrorsAsync(field)).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task YearSort_InAnArtistFolder_IsAllowed()
    {
        var item = await SeedItemAsync();

        using var response = await GetAsync(ApiRoutes.LibraryFolder(item.Placement!.ArtistFolderId) + "?sort=year&kind=audio&status=queued&limit=1&offset=0");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.BodyAsync<LibraryFolderViewDto>()).Children.Single().Name.ShouldBe("Metallica");
    }

    [Fact]
    public async Task UnfiledItems_ArePendingAndTriggerTheFiling()
    {
        var item = Asset.Create(Guid.NewGuid(), "old", ExportProvenance(Guid.NewGuid()), T0);
        await Factory.AddUnfiledAsync(item);

        using var response = await GetAsync(ApiRoutes.LibraryFolders);

        var view = await response.BodyAsync<LibraryFolderViewDto>();
        (view.PendingFiling, view.Children.Single().Name).ShouldBe((1, "Rick Astley"));
        await WaitUntilAsync(async () => (await Factory.FindAssetAsync(item.Id))!.IsFiled, Factory);
    }

    [Fact]
    public async Task Maintenance_Answers503()
    {
        Factory.Services.GetRequiredService<LibraryGate>().EnterMaintenance();
        try
        {
            using var response = await GetAsync(ApiRoutes.LibraryFolders);

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        }
        finally
        {
            Factory.Services.GetRequiredService<LibraryGate>().ExitMaintenance();
        }
    }
}

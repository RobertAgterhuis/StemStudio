using System.Net;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Library;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Library;
using static StemStudio.Api.Tests.Library.LibraryTestData;

namespace StemStudio.Api.Tests.Library;

/// <summary>022 T062: <c>GET /api/library/search</c> (backend-api 1.22.0, FR-030).</summary>
public sealed class LibrarySearchEndpointTests : ApiTestBase
{
    private Task<HttpResponseMessage> GetAsync(string query, string? ifNoneMatch = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{ApiRoutes.LibrarySearch}?{query}", UriKind.Relative));
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
    public async Task Search_GroupsHitsAndMatchesTheContract()
    {
        await SeedItemAsync();

        using var response = await GetAsync("q=" + Uri.EscapeDataString("  Métallica  "));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "searchLibrary");
        var result = await response.BodyAsync<LibrarySearchResultDto>();
        (result.Query, result.Tokens.Single(), result.Totals.Artists, result.Totals.Items).ShouldBe(("Métallica", "metallica", 1, 1));
        response.Headers.ETag!.ToString().ShouldBe(ApiRoutes.LibraryETag(result.Revision));
    }

    [Fact]
    public async Task NoMatches_IsAnEmptyResult()
    {
        using var response = await GetAsync("q=zzz");

        await ContractAssert.MatchesAsync(response, "searchLibrary");
        (await response.BodyAsync<LibrarySearchResultDto>()).Totals.ShouldBe(new SearchTotalsDto { Artists = 0, Albums = 0, Songs = 0, Items = 0 });
    }

    [Fact]
    public async Task SameRevision_Is304()
    {
        await SeedItemAsync();
        using var first = await GetAsync("q=metallica");

        using var again = await GetAsync("q=metallica", first.Headers.ETag!.ToString());

        again.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task Within_UnknownFolderIs404_KnownFolderFilters()
    {
        var item = await SeedItemAsync();

        using var unknown = await GetAsync($"q=a&within={FolderId.Artist("nobody")}");
        using var known = await GetAsync($"q=enter&within={item.Placement!.ArtistFolderId}&limit=5");

        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(unknown, "searchLibrary");
        (await known.BodyAsync<LibrarySearchResultDto>()).Songs.Single().Folder.Name.ShouldBe("Enter Sandman");
    }

    [Theory]
    [InlineData("", "q")]
    [InlineData("q=", "q")]
    [InlineData("q=%20%20%20", "q")]
    [InlineData("q=!!!", "q")]
    [InlineData("q=a&within=xx", "within")]
    [InlineData("q=a&limit=0", "limit")]
    [InlineData("q=a&limit=201", "limit")]
    public async Task InvalidQueries_Are422(string query, string field)
    {
        using var response = await GetAsync(query);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "searchLibrary");
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe(LibraryProblemTypes.Uri(LibraryProblemTypes.LibraryQueryInvalid));
        (await response.FieldErrorsAsync(field)).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task TooLongText_Is422_At101Characters()
    {
        using var ok = await GetAsync("q=" + new string('a', 100));
        using var tooLong = await GetAsync("q=" + new string('a', 101));

        (ok.StatusCode, tooLong.StatusCode).ShouldBe((HttpStatusCode.OK, HttpStatusCode.UnprocessableEntity));
    }

    [Fact]
    public async Task ManyWords_UseOnlyTheFirstEight()
    {
        using var response = await GetAsync("q=" + Uri.EscapeDataString("a b c d e f g h i j"));

        (await response.BodyAsync<LibrarySearchResultDto>()).Tokens.Count.ShouldBe(ContractLimits.LibrarySearchMaxTokens);
    }
}

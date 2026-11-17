using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Library;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Library;
using static StemStudio.Api.Tests.Library.LibraryInfoApi;
using static StemStudio.Api.Tests.Library.LibraryTestData;

namespace StemStudio.Api.Tests.Library;

/// <summary>
/// 022 T072 security self-review: library-info writes pass the 013 cross-site guard and the maintenance gate; names are
/// untrusted text that only becomes display names and hashed folder ids (never paths).
/// </summary>
public sealed partial class LibraryInfoSecurityTests : ApiTestBase
{
    public static TheoryData<string, bool> Writes => new()
    {
        { "PUT", true },
        { "DELETE", true },
        { "PUT", false },
        { "DELETE", false },
    };

    [Theory]
    [MemberData(nameof(Writes))]
    public async Task CrossSiteWrite_Is403_AndChangesNothing(string method, bool track)
    {
        var item = await FiledItemAsync();
        using var request = Request(method, track ? ApiRoutes.TrackLibraryInfo(item.TrackId!.Value) : ApiRoutes.AssetLibraryInfo(item.Id));
        request.Headers.Add("Origin", "http://evil.test");
        request.Headers.Add("Sec-Fetch-Site", "cross-site");

        using var response = await Client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Factory.FindAssetAsync(item.Id))!.LibraryInfo.ShouldBe(LibraryInfo.Automatic);
        (await Factory.FindTrackAsync(item.TrackId!.Value))!.LibraryInfo.ShouldBe(LibraryInfo.Automatic);
    }

    [Theory]
    [MemberData(nameof(Writes))]
    public async Task DuringMaintenance_Writes_Are503LibraryMaintenance(string method, bool track)
    {
        var item = await FiledItemAsync();
        var gate = Factory.Services.GetRequiredService<LibraryGate>();
        gate.EnterMaintenance();
        using var request = Request(method, track ? ApiRoutes.TrackLibraryInfo(item.TrackId!.Value) : ApiRoutes.AssetLibraryInfo(item.Id));

        using var response = await Client.SendAsync(request, Ct);

        gate.ExitMaintenance();
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe(LibraryCareProblemTypes.BaseUri + LibraryCareProblemTypes.LibraryMaintenance);
        (await Factory.FindAssetAsync(item.Id))!.LibraryInfo.ShouldBe(LibraryInfo.Automatic);
    }

    [Theory]
    [InlineData("../../outside")]
    [InlineData("..\\..\\Windows\\System32")]
    [InlineData("C:\\evil")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("'; DROP TABLE Assets; --")]
    public async Task PathOrMarkupLikeNames_AreStoredVerbatim_AndOnlyBecomeHashedFolderIds(string name)
    {
        var item = await FiledItemAsync();
        var before = Entries();

        using var response = await Client.PutTrackInfoAsync(item.TrackId!.Value, Names(name, name, name));

        var state = await response.BodyAsync<LibraryInfoStateDto>();
        (state.Artist.Effective, state.Album.Effective, state.Song.Effective).ShouldBe((name, name, name));
        var placement = (await (await Client.GetItemInfoAsync(item.Id)).BodyAsync<LibraryInfoStateDto>()).Placement!;
        new[] { placement.Artist.FolderId, placement.Album.FolderId, placement.Song.FolderId }.ShouldAllBe(id => FolderIdPattern().IsMatch(id));
        Entries().ShouldBe(before);
    }

    private static HttpRequestMessage Request(string method, string path) => new(new HttpMethod(method), new Uri(path, UriKind.Relative))
    {
        Content = method == "PUT" ? JsonContent.Create(Names(album: "Changed")) : null,
    };

    private string[] Entries() =>
        Directory.Exists(Factory.DataDir) ? [.. Directory.GetFileSystemEntries(Factory.DataDir, "*", SearchOption.AllDirectories).Where(path => !path.Contains(".db", StringComparison.Ordinal)).Order(StringComparer.Ordinal)] : [];

    private async Task<Asset> FiledItemAsync()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        var item = Asset.Create(Guid.NewGuid(), "drums", ExportProvenance(track.Id), T0);
        await Factory.FileAsync(item);
        return item;
    }

    [GeneratedRegex(ContractLimits.FolderIdPattern, RegexOptions.CultureInvariant)]
    private static partial Regex FolderIdPattern();
}

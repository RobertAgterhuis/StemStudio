using System.Net;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.Setlists;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Handouts;
using StemStudio.Domain.Handouts;

namespace StemStudio.Api.Tests.Handouts;

/// <summary>
/// Feature 020 T081: setlist handouts (FR-009): running order, entry notes (014 <c>note</c> "" -> null), a song that is not
/// completed is included with what it has, more than the limit is 409 <c>too-many-songs</c>, no entries is 409 <c>setlist-empty</c>.
/// </summary>
public sealed class SetlistHandoutTests : ApiTestBase
{
    [Fact]
    public async Task ASetlistHandout_FollowsTheRunningOrder_WithEntryNotes()
    {
        var (first, second) = (await Factory.SeedSongAsync("aaaaaaaaaaa"), await Factory.SeedAsync(SyncTestData.UrlTrack("bbbbbbbbbbb")));
        var setlist = await Client.CreateSetlistAsync("Band night", note: "Bring the capo.");
        (await Client.PutEntriesAsync(setlist.Id, SetlistTestData.Entry(second.Id, note: "Count in four."), SetlistTestData.Entry(first.Id))).EnsureSuccessStatusCode();

        using var response = await Client.PostHandoutAsync(new CreateHandoutRequest { Source = HandoutSourceDto.Setlist(setlist.Id), Family = HandoutFamilyDto.Overview });

        await ContractAssert.MatchesAsync(response, "createHandout");
        var asset = await response.BodyAsync<AssetDto>();
        (asset.Name, asset.Provenance.TrackId, asset.Provenance.TrackTitle).ShouldBe(("Band night - Band overview handout", (Guid?)null, "Band night"));
        var recipe = asset.Provenance.HandoutRecipe!;
        (recipe.KindLabel, recipe.SetlistId, recipe.SetlistName, recipe.TrackId).ShouldBe((HandoutKindDto.Setlist, (Guid?)setlist.Id, "Band night", (Guid?)null));
        var document = Factory.InsightsWorker.HandoutRequests.Single().Document;
        (document.Kind, document.Title, document.Subtitle).ShouldBe((HandoutKind.Setlist, "Band night", "Setlist - 2 songs"));
        document.Setlist!.Notes.ShouldBe("Bring the capo.");
        document.Setlist.Entries.Select(entry => (entry.Position, entry.Notes)).ShouldBe([(1, (string?)"Count in four."), (2, null)]);
        document.Songs.Select(song => song.Title).ShouldBe([second.Title, first.Title]);
        document.Songs[0].EntryNotes.ShouldBe("Count in four.");
        document.Songs[0].Include.ShouldContain(IngredientCode.SetlistNotes);
        document.Songs[1].Include.ShouldNotContain(IngredientCode.SetlistNotes);
        document.Songs[0].Sections.ShouldBeEmpty("the song that is not completed has no sections yet");
        document.Songs[1].Sections.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task EmptyNotes_AreNull()
    {
        var song = await Factory.SeedSongAsync();
        var setlist = await Client.CreateSetlistAsync("Rehearsal", note: string.Empty);
        (await Client.PutEntriesAsync(setlist.Id, SetlistTestData.Entry(song.Id))).EnsureSuccessStatusCode();

        (await Client.PostHandoutAsync(new CreateHandoutRequest { Source = HandoutSourceDto.Setlist(setlist.Id), Family = HandoutFamilyDto.Bass })).Dispose();

        var document = Factory.InsightsWorker.HandoutRequests.Single().Document;
        (document.Setlist!.Notes, document.Setlist.Entries.Single().Notes, document.Songs.Single().EntryNotes).ShouldBe(((string?)null, (string?)null, (string?)null));
    }

    [Fact]
    public async Task AnEmptySetlist_IsSetlistEmpty_AndAnUnknownOneIs404()
    {
        var setlist = await Client.CreateSetlistAsync("Empty");

        using var empty = await Client.PostHandoutAsync(new CreateHandoutRequest { Source = HandoutSourceDto.Setlist(setlist.Id), Family = HandoutFamilyDto.Bass });
        using var missing = await Client.PostHandoutAsync(new CreateHandoutRequest { Source = HandoutSourceDto.Setlist(Guid.NewGuid()), Family = HandoutFamilyDto.Bass });

        await ContractAssert.MatchesAsync(empty, "createHandout");
        (empty.StatusCode, missing.StatusCode).ShouldBe((HttpStatusCode.Conflict, HttpStatusCode.NotFound));
        (await empty.JsonAsync()).GetProperty("type").GetString().ShouldBe("https://stemstudio.local/problems/setlist-empty");
    }

    [Fact]
    public async Task MoreSongsThanTheLimit_IsTooManySongs()
    {
        using var factory = new ApiFactory { Settings = new Dictionary<string, string?> { ["StemStudio:MaxSetlistHandoutSongs"] = "2" } };
        using var client = factory.CreateClient();
        var songs = new[] { "aaaaaaaaaaa", "bbbbbbbbbbb", "ccccccccccc" };
        var tracks = new List<Guid>();
        foreach (var id in songs)
        {
            tracks.Add((await factory.SeedAsync(SyncTestData.CompletedTrack(id))).Id);
        }

        var setlist = await client.CreateSetlistAsync("Long night");
        (await client.PutEntriesAsync(setlist.Id, [.. tracks.Select(track => SetlistTestData.Entry(track))])).EnsureSuccessStatusCode();

        using var response = await client.PostHandoutAsync(new CreateHandoutRequest { Source = HandoutSourceDto.Setlist(setlist.Id), Family = HandoutFamilyDto.Drums });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.JsonAsync()).GetProperty("detail").GetString().ShouldBe("A setlist handout can hold up to 2 songs.");
    }

    [Fact]
    public async Task SetlistOptions_CountTheSongs()
    {
        var (first, second) = (await Factory.SeedSongAsync("aaaaaaaaaaa"), await Factory.SeedAsync(SyncTestData.CompletedTrack("bbbbbbbbbbb")));
        var setlist = await Client.CreateSetlistAsync("Band night");
        (await Client.PutEntriesAsync(setlist.Id, SetlistTestData.Entry(first.Id), SetlistTestData.Entry(second.Id))).EnsureSuccessStatusCode();

        using var response = await Client.GetAsync(new Uri(Contracts.ApiRoutes.HandoutOptions(HandoutSourceDto.Setlist(setlist.Id), HandoutFamilyDto.Overview), UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(response, "getHandoutOptions");
        var options = await response.BodyAsync<HandoutOptionsDto>();
        (options.Kind, options.Songs, options.EstimateSeconds).ShouldBe((HandoutKindDto.Setlist, 2, 8));
        options.Ingredients.Select(item => item.Code).ShouldContain(IngredientCodeDto.SetlistNotes);
        options.Ingredients.ShouldAllBe(item => item.Action == null);
    }
}

using System.Net;
using System.Net.Http.Json;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.SongSections;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Setlists;
using static StemStudio.Api.Tests.Setlists.SetlistTestData;

namespace StemStudio.Api.Tests.Setlists;

/// <summary>Feature 014 T088: setlist CRUD and entries (FR-015, FR-016, data-model 3.6).</summary>
public sealed class SetlistEndpointsTests : ApiTestBase
{
    [Fact]
    public async Task Create_Returns201WithLocation_AndAnEmptySetlist()
    {
        using var response = await Client.PostAsJsonAsync(ApiRoutes.Setlists, new SetlistCreateRequest { Name = "  Gig  ", Note = "Bring spare strings." }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        await ContractAssert.MatchesAsync(response, "createSetlist");
        var setlist = await response.BodyAsync<SetlistDto>();
        response.Headers.Location!.ToString().ShouldBe(ApiRoutes.Setlist(setlist.Id));
        (setlist.Name, setlist.Note, setlist.Entries.Count, setlist.TotalSeconds, setlist.RemovedEntries).ShouldBe(("Gig", "Bring spare strings.", 0, 0.0, 0));
    }

    [Theory]
    [InlineData("", "name")]
    [InlineData("123456789012345678901234567890123456789012345678901234567890123456789012345678901", "name")]
    public async Task Create_InvalidName_Is422(string name, string field)
    {
        using var response = await Client.PostAsJsonAsync(ApiRoutes.Setlists, new SetlistCreateRequest { Name = name }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "createSetlist");
        (await response.FieldErrorsAsync(field)).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ReplaceEntries_StoresOrderMixesAndNotes_AndTheTotal()
    {
        var (first, second) = (await Factory.SeedAsync(SectionTestData.Completed()), await Factory.SeedAsync(SectionTestData.Completed("aaaaaaaaaaa")));
        await Factory.SeedDetectedAsync(first.Id);
        var mix = await Factory.SeedMixAsync(first);
        var setlist = await Client.CreateSetlistAsync();

        using var response = await Client.PutEntriesAsync(setlist.Id, Entry(first.Id, mix.Id, "Start soft."), Entry(second.Id), Entry(first.Id, note: "Encore"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "replaceSetlistEntries");
        var stored = await response.BodyAsync<SetlistDto>();
        stored.Entries.Select(entry => (entry.Position, entry.TrackId, entry.MixId, entry.MixName, entry.Note)).ShouldBe(
            [(0, first.Id, (Guid?)mix.Id, "Band night", "Start soft."), (1, second.Id, null, null, ""), (2, first.Id, null, null, "Encore")]);
        stored.Entries[0].ShouldSatisfyAllConditions(
            entry => entry.SectionCount.ShouldBe(5),
            entry => entry.Bpm.ShouldBe(113.5),
            entry => entry.DurationSeconds.ShouldBe(213.04),
            entry => entry.HasLyrics.ShouldBeFalse());
        stored.TotalSeconds.ShouldBe(3 * 213.04, 1e-6);
    }

    [Fact]
    public async Task ReplaceEntries_KeepsSentIds_AndReorders()
    {
        var (first, second) = (await Factory.SeedAsync(SectionTestData.Completed()), await Factory.SeedAsync(SectionTestData.Completed("aaaaaaaaaaa")));
        var setlist = await Client.CreateSetlistAsync();
        using var created = await Client.PutEntriesAsync(setlist.Id, Entry(first.Id), Entry(second.Id));
        var entries = (await created.BodyAsync<SetlistDto>()).Entries;
        var (a, b) = (entries[0], entries[1]);

        using var reordered = await Client.PutEntriesAsync(setlist.Id, Entry(second.Id, id: b.Id), Entry(first.Id, id: a.Id));

        (await reordered.BodyAsync<SetlistDto>()).Entries.Select(entry => (entry.Id, entry.Position)).ShouldBe([(b.Id, 0), (a.Id, 1)]);
    }

    [Fact]
    public async Task ReplaceEntries_FieldErrors_ForUnknownTracksAndForeignMixes()
    {
        var (first, second) = (await Factory.SeedAsync(SectionTestData.Completed()), await Factory.SeedAsync(SectionTestData.Completed("aaaaaaaaaaa")));
        var mixOfSecond = await Factory.SeedMixAsync(second);
        var setlist = await Client.CreateSetlistAsync();

        using var response = await Client.PutEntriesAsync(setlist.Id, Entry(Guid.NewGuid()), Entry(first.Id, mixOfSecond.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "replaceSetlistEntries");
        (await response.FieldErrorsAsync("entries[0].trackId")).Single().ShouldContain("track-not-found");
        (await response.FieldErrorsAsync("entries[1].mixId")).Single().ShouldContain("mix-track-mismatch");
    }

    [Fact]
    public async Task ReplaceEntries_MoreThan100_Is422()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        var setlist = await Client.CreateSetlistAsync();

        using var response = await Client.PutEntriesAsync(setlist.Id, Enumerable.Range(0, 101).Select(_ => Entry(track.Id)).ToArray());

        (await response.FieldErrorsAsync("entries")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task UpdateAndGet_RenameAndNote()
    {
        var setlist = await Client.CreateSetlistAsync();

        using var renamed = await Client.PatchAsJsonAsync(ApiRoutes.Setlist(setlist.Id), new SetlistUpdateRequest { Name = "Saturday" }, Ct);
        using var invalid = await Client.PatchAsJsonAsync(ApiRoutes.Setlist(setlist.Id), new SetlistUpdateRequest { Note = new string('n', 2001) }, Ct);
        using var read = await Client.GetAsync(new Uri(ApiRoutes.Setlist(setlist.Id), UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(renamed, "updateSetlist");
        invalid.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(read, "getSetlist");
        var stored = await read.BodyAsync<SetlistDto>();
        (stored.Name, stored.Note).ShouldBe(("Saturday", "Bring the capo."));
    }

    [Fact]
    public async Task List_IsNewestFirst_WithSongsAndTotals()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        var older = await Client.CreateSetlistAsync("Older");
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var newer = await Client.CreateSetlistAsync("Newer");
        (await Client.PutEntriesAsync(newer.Id, Entry(track.Id), Entry(track.Id))).Dispose();

        using var response = await Client.GetAsync(new Uri(ApiRoutes.Setlists, UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(response, "listSetlists");
        var list = await response.BodyAsync<List<SetlistSummaryDto>>();
        list.Select(item => (item.Id, item.Songs)).ShouldBe([(newer.Id, 2), (older.Id, 0)]);
        list[0].TotalSeconds.ShouldBe(2 * 213.04, 1e-6);
    }

    [Fact]
    public async Task Delete_RemovesTheSetlist_AndUnknownIdsAre404()
    {
        var setlist = await Client.CreateSetlistAsync();

        using var deleted = await Client.DeleteAsync(new Uri(ApiRoutes.Setlist(setlist.Id), UriKind.Relative), Ct);
        using var again = await Client.DeleteAsync(new Uri(ApiRoutes.Setlist(setlist.Id), UriKind.Relative), Ct);
        using var get = await Client.GetAsync(new Uri(ApiRoutes.Setlist(setlist.Id), UriKind.Relative), Ct);
        using var patch = await Client.PatchAsJsonAsync(ApiRoutes.Setlist(setlist.Id), new SetlistUpdateRequest { Name = "x" }, Ct);
        using var entries = await Client.PutEntriesAsync(setlist.Id);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (again.StatusCode, get.StatusCode, patch.StatusCode, entries.StatusCode).ShouldBe((HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task TrackDelete_RemovesItsEntries_AndTheNoticeIsAcknowledgedOnce()
    {
        var (first, second) = (await Factory.SeedAsync(SectionTestData.Completed()), await Factory.SeedAsync(SectionTestData.Completed("aaaaaaaaaaa")));
        var setlist = await Client.CreateSetlistAsync();
        (await Client.PutEntriesAsync(setlist.Id, Entry(first.Id), Entry(second.Id), Entry(first.Id))).Dispose();

        (await Client.DeleteAsync(new Uri(ApiRoutes.Track(first.Id), UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var noticed = await Client.GetAsync(new Uri(ApiRoutes.SetlistAcknowledgingRemoved(setlist.Id), UriKind.Relative), Ct);
        var shown = await noticed.BodyAsync<SetlistDto>();
        (shown.RemovedEntries, shown.Entries.Single().TrackId).ShouldBe((2, second.Id));
        using var after = await Client.GetAsync(new Uri(ApiRoutes.Setlist(setlist.Id), UriKind.Relative), Ct);
        (await after.BodyAsync<SetlistDto>()).RemovedEntries.ShouldBe(0);
    }

    [Fact]
    public async Task MixDelete_ClearsTheEntryMix()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        var mix = await Factory.SeedMixAsync(track);
        var setlist = await Client.CreateSetlistAsync();
        (await Client.PutEntriesAsync(setlist.Id, Entry(track.Id, mix.Id))).Dispose();

        (await Client.DeleteAsync(new Uri(ApiRoutes.Mix(mix.Id), UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var read = await Client.GetAsync(new Uri(ApiRoutes.Setlist(setlist.Id), UriKind.Relative), Ct);
        var entry = (await read.BodyAsync<SetlistDto>()).Entries.Single();
        (entry.MixId, entry.MixName).ShouldBe(((Guid?)null, (string?)null));
    }
}

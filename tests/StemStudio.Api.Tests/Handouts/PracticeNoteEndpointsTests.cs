using System.Net;
using System.Net.Http.Json;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Handouts;

namespace StemStudio.Api.Tests.Handouts;

/// <summary>Feature 020 T066: practice notes per family (FR-015): 4,000 limit, control characters removed, empty deletes (204).</summary>
public sealed class PracticeNoteEndpointsTests : ApiTestBase
{
    [Fact]
    public async Task ANote_IsStoredCleaned_ListedAndReplaced()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        using var first = await PutAsync(track.Id, "bass", "  Keep the eighths even.\r\nMute\u0007 the open A.  ");
        using var other = await PutAsync(track.Id, "melodic", "Breathe at bar 9.");
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        using var replaced = await PutAsync(track.Id, "bass", "Ghost notes.");
        using var list = await Client.GetAsync(new Uri(ApiRoutes.PracticeNotes(track.Id), UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(first, "setPracticeNote");
        await ContractAssert.MatchesAsync(list, "getPracticeNotes");
        (await first.BodyAsync<PracticeNoteDto>()).Text.ShouldBe("Keep the eighths even.\nMute the open A.");
        var notes = (await list.BodyAsync<PracticeNotesDto>()).Notes;
        notes.Select(note => (note.Family, note.Text)).ShouldBe([(HandoutFamilyDto.Bass, "Ghost notes."), (HandoutFamilyDto.Melodic, "Breathe at bar 9.")]);
        notes[0].UpdatedAt.ShouldBe(ApiFactory.Start.AddMinutes(5));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\t ")]
    public async Task AnEmptyText_DeletesTheNote(string text)
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        (await PutAsync(track.Id, "drums", "Ghost notes.")).Dispose();

        using var deleted = await PutAsync(track.Id, "drums", text);
        using var again = await PutAsync(track.Id, "drums", text);

        (deleted.StatusCode, again.StatusCode).ShouldBe((HttpStatusCode.NoContent, HttpStatusCode.NoContent));
        await ContractAssert.MatchesAsync(deleted, "setPracticeNote");
        (await (await Client.GetAsync(new Uri(ApiRoutes.PracticeNotes(track.Id), UriKind.Relative), Ct)).BodyAsync<PracticeNotesDto>()).Notes.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheLimitFamilyAndTrack_AreChecked()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        using var exact = await PutAsync(track.Id, "keys", new string('a', ContractLimits.MaxPracticeNoteLength));
        using var tooLong = await PutAsync(track.Id, "keys", new string('a', ContractLimits.MaxPracticeNoteLength + 1));
        using var family = await PutAsync(track.Id, "tuba", "x");
        using var missingPut = await PutAsync(Guid.NewGuid(), "keys", "x");
        using var missingGet = await Client.GetAsync(new Uri(ApiRoutes.PracticeNotes(Guid.NewGuid()), UriKind.Relative), Ct);
        using var noBody = await Client.PutAsJsonAsync(ApiRoutes.PracticeNote(track.Id, HandoutFamilyDto.Keys), new { }, Ct);

        (exact.StatusCode, tooLong.StatusCode, family.StatusCode, missingPut.StatusCode, missingGet.StatusCode).ShouldBe((HttpStatusCode.OK, HttpStatusCode.UnprocessableEntity, HttpStatusCode.UnprocessableEntity, HttpStatusCode.NotFound, HttpStatusCode.NotFound));
        foreach (var response in new[] { tooLong, family, missingPut })
        {
            await ContractAssert.MatchesAsync(response, "setPracticeNote");
        }

        await ContractAssert.MatchesAsync(missingGet, "getPracticeNotes");
        (await tooLong.FieldErrorsAsync("text")).ShouldNotBeEmpty();
        ((int)noBody.StatusCode).ShouldBeInRange(400, 422);
    }

    [Fact]
    public async Task NotesAreDeletedWithTheirTrack()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        (await PutAsync(track.Id, "vocals", "Breathe.")).Dispose();

        (await Client.DeleteAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct)).Dispose();

        (await PutAsync(track.Id, "vocals", "x")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConcurrentPuts_OfOneNote_AnswerOnly200Or204()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        var codes = new List<HttpStatusCode>();
        for (var round = 0; round < 12; round++)
        {
            var family = round % 2 == 0 ? "bass" : "drums";
            (await PutAsync(track.Id, family, string.Empty)).Dispose();
            var texts = new[] { "first", "second", round % 3 == 0 ? string.Empty : "third", "fourth" };
            foreach (var put in await Task.WhenAll(texts.Select(text => PutAsync(track.Id, family, text))))
            {
                codes.Add(put.StatusCode);
                put.Dispose();
            }
        }

        codes.ShouldAllBe(code => code == HttpStatusCode.OK || code == HttpStatusCode.NoContent);
        var notes = (await (await Client.GetAsync(new Uri(ApiRoutes.PracticeNotes(track.Id), UriKind.Relative), Ct)).BodyAsync<PracticeNotesDto>()).Notes;
        notes.Count.ShouldBeLessThanOrEqualTo(2);
    }

    private Task<HttpResponseMessage> PutAsync(Guid trackId, string family, string text) =>
        Client.PutAsJsonAsync($"{ApiRoutes.PracticeNotes(trackId)}/{family}", new SetPracticeNoteRequest { Text = text }, Ct);
}

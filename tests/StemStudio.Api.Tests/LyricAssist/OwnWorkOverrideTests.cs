using System.Net;
using System.Net.Http.Json;
using StemStudio.Agent.LyricAssist;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Contracts.Lyrics;
using StemStudio.Domain.Lyrics;

namespace StemStudio.Api.Tests.LyricAssist;

/// <summary>023 T074: own-work override (FR-031, SC-005): confirm, covers one track only, never the verbatim hold-back nor the imitation refusal, revoke.</summary>
public sealed class OwnWorkOverrideTests : AssistTestBase
{
    [Fact]
    public async Task Confirm_ThenTheSheetRuns_AndRevokeRefusesAgain()
    {
        var song = await SeedTrackAsync("aaaaaaaaaaa", "t-fake-song.en.txt");
        var sheet = await SheetAsync(AssistFixtureText.Sheet("not-own.en.txt"), title: "Lanterns");

        using var confirmed = await ConfirmAsync(sheet.Id, Request(song.Id));
        var assist = await RunToEndAsync(sheet.Id, Check());
        using var sheetNow = await Client.GetAsync(new Uri(ApiRoutes.LyricsSheet(sheet.Id), UriKind.Relative), Ct);
        using var revoked = await Client.DeleteAsync(new Uri(ApiRoutes.LyricsOwnWorkOverride(sheet.Id), UriKind.Relative), Ct);
        using var revokedAgain = await Client.DeleteAsync(new Uri(ApiRoutes.LyricsOwnWorkOverride(sheet.Id), UriKind.Relative), Ct);
        using var refused = await CreateAsync(sheet.Id, Check());

        (await confirmed.BodyAsync<OwnWorkOverrideDto>()).ShouldSatisfyAllConditions(
            o => o.MatchedTrackId.ShouldBe(song.Id), o => o.ConfirmedBy.ShouldBe("Robin"), o => o.StatementVersion.ShouldBe("own-work.v1"));
        assist.Status.ShouldBe(AssistStatusDto.Completed);
        (await sheetNow.BodyAsync<LyricsSheetDto>()).Assist.OwnWorkOverride!.MatchedTrackTitle.ShouldBe(song.Title);
        (revoked.StatusCode, revokedAgain.StatusCode).ShouldBe((HttpStatusCode.NoContent, HttpStatusCode.NotFound));
        (await refused.JsonAsync()).GetProperty("code").GetString().ShouldBe("not-own-lyrics");
        await ContractAssert.MatchesAsync(confirmed, "confirmOwnWork");
        await ContractAssert.MatchesAsync(revoked, "revokeOwnWork");
        await ContractAssert.MatchesAsync(revokedAgain, "revokeOwnWork");
    }

    [Fact]
    public async Task Override_CoversOnlyItsTrack()
    {
        var song = await SeedTrackAsync("aaaaaaaaaaa", "t-fake-song.en.txt");
        var song2 = await SeedTrackAsync("bbbbbbbbbbb", "t-fake-song-2.en.txt");
        var sheet = await SheetAsync(AssistFixtureText.Sheet("not-own-two-tracks.en.txt"));

        (await ConfirmAsync(sheet.Id, Request(song.Id))).Dispose();
        using var refused = await CreateAsync(sheet.Id, Check());

        var problem = await refused.JsonAsync();
        (problem.GetProperty("code").GetString(), problem.GetProperty("matchedTrackId").GetGuid()).ShouldBe(("not-own-lyrics", song2.Id));
    }

    [Fact]
    public async Task Override_NeverDisablesVerbatimOrImitation()
    {
        var song = await SeedTrackAsync("aaaaaaaaaaa", "t-fake-song.en.txt");
        var sheet = await SheetAsync(AssistFixtureText.Sheet("not-own.en.txt"));
        (await ConfirmAsync(sheet.Id, Request(song.Id))).Dispose();
        Assistants.Assistant = new ScriptedAssistant((_, sink, ct) => sink.BlockCompletedAsync(new LyricBlockResult
        {
            BlockIndex = 0,
            Suggestions = [new AssistantSuggestion
            {
                Category = SuggestionCategoryDto.Alternative, Type = SuggestionTypeDto.WriteAlternative, Severity = SuggestionSeverityDto.Idea,
                Anchor = new AnchorDto { Line = 1, Start = 0, Length = 1, Original = AssistFixtureText.Sheet("not-own.en.txt")[..1] },
                Operation = SuggestionOperationDto.Replace, Proposal = "we light the paper lanterns down by the river", Explanation = "x",
            }],
        }, ct));

        using var imitation = await CreateAsync(sheet.Id, new CreateLyricAssistRequest { Kind = AssistKindDto.Alternatives, ExpectedRevision = 1, LineFrom = 1, LineTo = 1, Instruction = "in the style of someone" });
        var assist = await RunToEndAsync(sheet.Id, new CreateLyricAssistRequest { Kind = AssistKindDto.Alternatives, ExpectedRevision = 1, LineFrom = 1, LineTo = 1 });

        imitation.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (assist.Suggestions.Count, assist.Discarded.Verbatim).ShouldBe((0, 1));
    }

    [Fact]
    public async Task Confirm_RefusesOutdatedUnticketedMismatchedAndUnnamed()
    {
        var song = await SeedTrackAsync("aaaaaaaaaaa", "t-fake-song.en.txt");
        var other = await SeedTrackAsync("ccccccccccc", "t-fake-cliches.en.txt");
        var sheet = await SheetAsync(AssistFixtureText.Sheet("not-own.en.txt"));
        var own = await SheetAsync("the cat sat on the mat today");

        using var outdated = await ConfirmAsync(sheet.Id, Request(song.Id) with { StatementVersion = "own-work.v0" });
        using var unticked = await ConfirmAsync(sheet.Id, Request(song.Id) with { Confirm = false });
        using var wrongTrack = await ConfirmAsync(sheet.Id, Request(other.Id));
        using var noMatch = await ConfirmAsync(own.Id, Request(song.Id));
        using var unnamed = await ConfirmAsync(sheet.Id, Request(song.Id) with { ConfirmedBy = " " });
        using var missing = await ConfirmAsync(Guid.NewGuid(), Request(song.Id));

        (await Code(outdated), await Code(wrongTrack), await Code(noMatch)).ShouldBe(("statement-outdated", "override-not-applicable", "override-not-applicable"));
        (await unticked.FieldErrorsAsync("confirm")).ShouldNotBeEmpty();
        (await unnamed.FieldErrorsAsync("confirmedBy")).ShouldNotBeEmpty();
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(outdated, "confirmOwnWork");
        await ContractAssert.MatchesAsync(unnamed, "confirmOwnWork");
    }

    private static OwnWorkConfirmRequest Request(Guid trackId) => new() { MatchedTrackId = trackId, ConfirmedBy = " Robin ", StatementVersion = "own-work.v1", Confirm = true };

    private Task<HttpResponseMessage> ConfirmAsync(Guid sheetId, OwnWorkConfirmRequest request) =>
        Client.PutAsJsonAsync(new Uri(ApiRoutes.LyricsOwnWorkOverride(sheetId), UriKind.Relative), request, Ct);

    private static async Task<string?> Code(HttpResponseMessage response) => (await response.JsonAsync()).GetProperty("code").GetString();

    private Task<Domain.Tracks.Track> SeedTrackAsync(string videoId, string corpusFile)
    {
        var track = SyncTestData.CompletedTrack(videoId);
        track.EnsureLyrics(SyncTestData.T0).Paste(LyricsDocument.FromUserText(AssistFixtureText.Corpus(corpusFile), null), SyncTestData.T0);
        return Factory.SeedAsync(track);
    }
}

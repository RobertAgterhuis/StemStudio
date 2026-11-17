using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Contracts.Lyrics;
using StemStudio.Domain.Lyrics.Assist;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.Lyrics;

/// <summary>023 T016: 010 compatibility of sheet saves (FR-007, FR-008, FR-024, SC-009): revision, expectedRevision, re-anchoring.</summary>
public sealed class LyricsRevisionTests : ApiTestBase
{
    private const string Text = "the cat sat\non the mat";

    [Fact]
    public async Task Create_StartsAtRevisionOneWithDefaultAssistInfo()
    {
        var sheet = await CreateAsync();

        (sheet.Revision, sheet.Assist.Sensitivity, sheet.Assist.OpenSuggestions, sheet.Assist.CanUndo).ShouldBe((1, SensitivityDto.Balanced, 0, false));
        var revisions = await Factory.WithScopeAsync(sp => sp.GetRequiredService<ILyricsRevisionRepository>().ListAsync(sheet.Id, Ct));
        revisions.Single().Cause.ShouldBe(RevisionCause.Created);
    }

    [Fact]
    public async Task SaveWithoutExpectedRevision_KeepsLastWriteWinsAndCountsTheRevision()
    {
        var sheet = await CreateAsync();

        using var first = await PutAsync(sheet.Id, Input("first"));
        using var second = await PutAsync(sheet.Id, Input("second"));

        (await second.BodyAsync<LyricsSheetDto>()).ShouldSatisfyAllConditions(s => s.Revision.ShouldBe(3), s => s.Text.ShouldBe("second"));
        await ContractAssert.MatchesAsync(second, "updateLyricsSheet");
        using var get = await Client.GetAsync(new Uri(ApiRoutes.LyricsSheet(sheet.Id), UriKind.Relative), Ct);
        await ContractAssert.MatchesAsync(get, "getLyricsSheet");
        using var list = await Client.GetAsync(new Uri(ApiRoutes.LyricsSheets, UriKind.Relative), Ct);
        (await list.BodyAsync<List<LyricsSheetDto>>()).Single().Revision.ShouldBe(3);
    }

    [Fact]
    public async Task StaleExpectedRevision_Is409RevisionConflict()
    {
        var sheet = await CreateAsync();
        using var ok = await PutAsync(sheet.Id, Input("next") with { ExpectedRevision = 1 });

        using var stale = await PutAsync(sheet.Id, Input("lost") with { ExpectedRevision = 1 });

        (await ok.BodyAsync<LyricsSheetDto>()).Revision.ShouldBe(2);
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await stale.JsonAsync();
        await ContractAssert.MatchesAsync(stale, "updateLyricsSheet");
        (problem.GetProperty("code").GetString(), problem.GetProperty("currentRevision").GetInt32(), problem.GetProperty("type").GetString())
            .ShouldBe(("revision-conflict", 2, "https://stemstudio.local/problems/revision-conflict"));
        problem.GetRawText().ShouldNotContain("lost");
    }

    [Fact]
    public async Task Save_ReanchorsOpenSuggestionsAndMarksGoneOnesStale()
    {
        var sheet = await CreateAsync();
        var (moved, gone) = await SeedSuggestionsAsync(sheet.Id);

        using var saved = await PutAsync(sheet.Id, Input("intro\nthe cat sat\non the rug"));

        (await saved.BodyAsync<LyricsSheetDto>()).Assist.OpenSuggestions.ShouldBe(1);
        var stored = await SuggestionsAsync(sheet.Id);
        stored.Single(s => s.Id == moved).Anchor.ShouldBe(new Anchor(2, 4, 3, "cat"));
        stored.Single(s => s.Id == gone).Status.ShouldBe(SuggestionStatus.Stale);
    }

    [Fact]
    public async Task LanguageChange_MakesOpenSuggestionsStale()
    {
        var sheet = await CreateAsync();
        await SeedSuggestionsAsync(sheet.Id);

        using var saved = await PutAsync(sheet.Id, Input(Text) with { Language = "nl" });

        (await SuggestionsAsync(sheet.Id)).ShouldAllBe(s => s.Status == SuggestionStatus.Stale);
    }

    [Fact]
    public async Task Save_PrunesAssistedLinesTheUserEdited()
    {
        var sheet = await CreateAsync();
        await Factory.WithScopeAsync(async sp =>
        {
            var sheets = sp.GetRequiredService<ILyricsSheetRepository>();
            var stored = (await sheets.FindAsync(sheet.Id, Ct))!;
            stored.ApplyAccepted(SuggestionApplier.Apply(stored.Title, stored.Text, [new ApplicableSuggestion(Guid.NewGuid(), new Anchor(1, 4, 3, "cat"), SuggestionOperation.Replace, "dog")]), DateTime.UtcNow);
            await sheets.SaveAsync(Ct);
            return true;
        });
        using var before = await Client.GetAsync(new Uri(ApiRoutes.LyricsSheet(sheet.Id), UriKind.Relative), Ct);
        (await before.BodyAsync<LyricsSheetDto>()).Assist.ShouldSatisfyAllConditions(a => a.AssistedLines.Single().Line.ShouldBe(1), a => a.CanUndo.ShouldBeTrue());

        using var saved = await PutAsync(sheet.Id, Input("the dog sat down\non the mat"));

        (await saved.BodyAsync<LyricsSheetDto>()).Assist.ShouldSatisfyAllConditions(a => a.AssistedLines.ShouldBeEmpty(), a => a.CanUndo.ShouldBeFalse());
    }

    [Fact]
    public async Task Delete_CascadesToAssistsAndCancelsTheActiveOne()
    {
        var sheet = await CreateAsync();
        await SeedSuggestionsAsync(sheet.Id);

        using var deleted = await Client.DeleteAsync(new Uri(ApiRoutes.LyricsSheet(sheet.Id), UriKind.Relative), Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await SuggestionsAsync(sheet.Id)).ShouldBeEmpty();
        (await Factory.WithScopeAsync(sp => sp.GetRequiredService<ILyricAssistRepository>().ActiveAsync(Ct))).ShouldBeEmpty();
    }

    private static LyricsInput Input(string text) => new() { Title = "Song", Language = "en", Text = text };

    private async Task<LyricsSheetDto> CreateAsync()
    {
        using var response = await Client.PostAsJsonAsync(new Uri(ApiRoutes.LyricsSheets, UriKind.Relative), Input(Text), Ct);
        await ContractAssert.MatchesAsync(response, "createLyricsSheet");
        return await response.BodyAsync<LyricsSheetDto>();
    }

    private Task<HttpResponseMessage> PutAsync(Guid id, LyricsInput input) =>
        Client.PutAsJsonAsync(new Uri(ApiRoutes.LyricsSheet(id), UriKind.Relative), input, Ct);

    private Task<IReadOnlyList<LyricSuggestion>> SuggestionsAsync(Guid sheetId) =>
        Factory.WithScopeAsync(sp => sp.GetRequiredService<ILyricSuggestionRepository>().ForSheetAsync(sheetId, null, null, Ct));

    private Task<(Guid Moved, Guid Gone)> SeedSuggestionsAsync(Guid sheetId) => Factory.WithScopeAsync(async sp =>
    {
        var sheet = (await sp.GetRequiredService<ILyricsSheetRepository>().FindAsync(sheetId, Ct))!;
        var assist = AssistRun.Create(Guid.NewGuid(), sheet, new AssistRequest { Kind = AssistKind.Check }, DateTime.UtcNow);
        await sp.GetRequiredService<ILyricAssistRepository>().AddAsync(assist, Ct);
        var moved = Draft(new Anchor(1, 4, 3, "cat"));
        var gone = Draft(new Anchor(2, 7, 3, "mat"));
        var suggestions = sp.GetRequiredService<ILyricSuggestionRepository>();
        var created = new[] { LyricSuggestion.Create(Guid.NewGuid(), assist, moved, DateTime.UtcNow), LyricSuggestion.Create(Guid.NewGuid(), assist, gone, DateTime.UtcNow) };
        suggestions.AddRange(created);
        await suggestions.SaveAsync(Ct);
        return (created[0].Id, created[1].Id);
    });

    private static SuggestionDraft Draft(Anchor anchor) => new()
    {
        Type = SuggestionType.SpellingTypo,
        Severity = SuggestionSeverity.Error,
        Anchor = anchor,
        Operation = SuggestionOperation.Replace,
        Proposal = "x",
        Explanation = "e",
        Source = SuggestionSource.Dictionary,
    };
}

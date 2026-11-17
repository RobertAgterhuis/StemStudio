using System.Net;
using System.Net.Http.Json;
using StemStudio.Agent.LyricAssist;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Contracts.Lyrics;

namespace StemStudio.Api.Tests.LyricAssist;

/// <summary>023 T029: suggestions API (FR-006 - FR-012): list, accept, reject, intentional, marks, words, undo, revisions, sheet settings.</summary>
public sealed class SuggestionEndpointTests : AssistTestBase
{
    private const string Text = "the cat sat\non the mat\nthe dog";

    [Fact]
    public async Task List_FiltersAndDefaultsToOpen()
    {
        var (sheet, _) = await SeededAsync(Replace(1, 4, "cat", "dog"), Replace(2, 7, "mat", "rug") with { Type = SuggestionTypeDto.GrammarOther, Category = SuggestionCategoryDto.Grammar });

        using var open = await GetAsync(ApiRoutes.LyricsSuggestions(sheet.Id));
        using var grammar = await GetAsync(ApiRoutes.LyricsSuggestions(sheet.Id, SuggestionStatusDto.Open, SuggestionCategoryDto.Grammar));
        using var invalid = await GetAsync(ApiRoutes.LyricsSuggestions(sheet.Id) + "?status=nope");
        using var invalidCategory = await GetAsync(ApiRoutes.LyricsSuggestions(sheet.Id) + "?category=nope");
        using var missing = await GetAsync(ApiRoutes.LyricsSuggestions(Guid.NewGuid()));

        (await open.BodyAsync<SuggestionListDto>()).Suggestions.Count.ShouldBe(2);
        (await grammar.BodyAsync<SuggestionListDto>()).Suggestions.Single().Category.ShouldBe(SuggestionCategoryDto.Grammar);
        (await invalid.FieldErrorsAsync("status")).ShouldNotBeEmpty();
        (await invalidCategory.FieldErrorsAsync("category")).ShouldNotBeEmpty();
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(open, "listLyricSuggestions");
    }

    [Fact]
    public async Task AcceptMany_IsOneRevisionAndOneUndoStep()
    {
        var (sheet, ids) = await SeededAsync(Replace(1, 4, "cat", "dog"), Replace(2, 7, "mat", "rug"));

        using var accepted = await PostAsync(ApiRoutes.LyricsSuggestionsAccept(sheet.Id), new SuggestionDecisionRequest { SuggestionIds = ids, ExpectedRevision = 1 });

        var result = await accepted.BodyAsync<AcceptResultDto>();
        (result.Revision, result.Text, result.Applied.Count, result.Assist.CanUndo).ShouldBe((2, "the dog sat\non the rug\nthe dog", 2, true));
        result.Assist.AssistedLines.Select(l => l.Line).ShouldBe([1, 2]);
        await ContractAssert.MatchesAsync(accepted, "acceptLyricSuggestions");

        using var undo = await PostAsync(ApiRoutes.LyricsUndo(sheet.Id), new RevisionRequest { ExpectedRevision = 2 });
        var undone = await undo.BodyAsync<AcceptResultDto>();
        (undone.Revision, undone.Text, undone.Reopened!.Count, undone.Assist.AssistedLines.Count).ShouldBe((3, Text, 2, 0));
        await ContractAssert.MatchesAsync(undo, "undoLyricSuggestions");
        using var again = await PostAsync(ApiRoutes.LyricsUndo(sheet.Id), new RevisionRequest { ExpectedRevision = 3 });
        (await Code(again)).ShouldBe("nothing-to-undo");
    }

    [Fact]
    public async Task Accept_RefusesOverlapTooLongStaleRevisionAndClosedOnes()
    {
        var (sheet, ids) = await SeededAsync(Replace(1, 0, "the cat", "a cat"), Replace(1, 4, "cat", "dog"), Replace(3, 4, "dog", new string('x', 400)));

        using var overlap = await PostAsync(ApiRoutes.LyricsSuggestionsAccept(sheet.Id), new SuggestionDecisionRequest { SuggestionIds = [ids[0], ids[1]], ExpectedRevision = 1 });
        using var stale = await PostAsync(ApiRoutes.LyricsSuggestionsAccept(sheet.Id), new SuggestionDecisionRequest { SuggestionIds = [ids[0]], ExpectedRevision = 5 });
        using var none = await PostAsync(ApiRoutes.LyricsSuggestionsAccept(sheet.Id), new SuggestionDecisionRequest { SuggestionIds = [], ExpectedRevision = 1 });
        using var unknown = await PostAsync(ApiRoutes.LyricsSuggestionsAccept(sheet.Id), new SuggestionDecisionRequest { SuggestionIds = [Guid.NewGuid()], ExpectedRevision = 1 });
        using var missingSheet = await PostAsync(ApiRoutes.LyricsSuggestionsAccept(Guid.NewGuid()), new SuggestionDecisionRequest { SuggestionIds = [ids[0]], ExpectedRevision = 1 });
        using var rejected = await PostAsync(ApiRoutes.LyricsSuggestionsReject(sheet.Id), new SuggestionDecisionRequest { SuggestionIds = [ids[1]], ExpectedRevision = 1 });
        using var closed = await PostAsync(ApiRoutes.LyricsSuggestionsAccept(sheet.Id), new SuggestionDecisionRequest { SuggestionIds = [ids[1]], ExpectedRevision = 1 });

        ((await overlap.JsonAsync()).GetProperty("suggestionIds").GetArrayLength(), await Code(overlap)).ShouldBe((2, "overlapping-suggestions"));
        (await Code(stale), none.StatusCode, unknown.StatusCode, missingSheet.StatusCode).ShouldBe(("revision-conflict", HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.NotFound));
        (await rejected.BodyAsync<SuggestionListDto>()).Suggestions.Single().Status.ShouldBe(SuggestionStatusDto.Rejected);
        (await Code(closed)).ShouldBe("suggestion-not-open");
        await ContractAssert.MatchesAsync(overlap, "acceptLyricSuggestions");
        await ContractAssert.MatchesAsync(rejected, "rejectLyricSuggestions");
        await ContractAssert.MatchesAsync(closed, "acceptLyricSuggestions");
    }

    [Fact]
    public async Task Accept_TooLongIs422AndStaysOpen()
    {
        var text = Text + "\n" + string.Join('\n', Enumerable.Range(0, 40).Select(_ => new string('z', 99)));
        var (sheet, ids) = await SeededAsync(text, Replace(1, 4, "cat", new string('y', 100)));

        using var tooLong = await PostAsync(ApiRoutes.LyricsSuggestionsAccept(sheet.Id), new SuggestionDecisionRequest { SuggestionIds = ids, ExpectedRevision = 1 });
        using var open = await GetAsync(ApiRoutes.LyricsSuggestions(sheet.Id));

        (tooLong.StatusCode, (await tooLong.JsonAsync()).GetProperty("type").GetString()).ShouldBe((HttpStatusCode.UnprocessableEntity, "https://stemstudio.local/problems/too-long"));
        (await open.BodyAsync<SuggestionListDto>()).Suggestions.Single().SuggestionId.ShouldBe(ids[0]);
        await ContractAssert.MatchesAsync(tooLong, "acceptLyricSuggestions");
    }

    [Fact]
    public async Task AcceptAfterEdit_SkipsStaleOnesAndUndoIsBlocked()
    {
        var (sheet, ids) = await SeededAsync(Replace(1, 4, "cat", "dog"), Replace(2, 7, "mat", "rug"));
        await PutSheetAsync(sheet.Id, "the cow sat\non the mat\nthe dog");

        using var accepted = await PostAsync(ApiRoutes.LyricsSuggestionsAccept(sheet.Id), new SuggestionDecisionRequest { SuggestionIds = ids, ExpectedRevision = 2 });
        var result = await accepted.BodyAsync<AcceptResultDto>();
        (result.Applied.Single(), result.Stale.Single(), result.Text).ShouldBe((ids[1], ids[0], "the cow sat\non the rug\nthe dog"));

        await PutSheetAsync(sheet.Id, "the cow sat\non the rug!\nthe dog\nnew");
        using var blocked = await PostAsync(ApiRoutes.LyricsUndo(sheet.Id), new RevisionRequest { ExpectedRevision = 4 });
        ((await blocked.JsonAsync()).GetProperty("changedLines").GetInt32(), await Code(blocked)).ShouldBe((2, "undo-blocked-by-edit"));
        await ContractAssert.MatchesAsync(blocked, "undoLyricSuggestions");

        using var revisions = await GetAsync(ApiRoutes.LyricsRevisions(sheet.Id));
        var list = await revisions.BodyAsync<List<LyricsRevisionSummaryDto>>();
        list.Select(r => (r.Revision, r.Cause)).ShouldBe([(4, RevisionCauseDto.UserSave), (3, RevisionCauseDto.Accept), (2, RevisionCauseDto.UserSave), (1, RevisionCauseDto.Created)]);
        await ContractAssert.MatchesAsync(revisions, "listLyricsRevisions");

        using var restore = await PostAsync(ApiRoutes.LyricsRevisionRestore(sheet.Id, 1), new RevisionRequest { ExpectedRevision = 4 });
        var restored = await restore.BodyAsync<AcceptResultDto>();
        (restored.Revision, restored.Text).ShouldBe((5, Text));
        restored.Reopened.ShouldBe([ids[0]]);
        await ContractAssert.MatchesAsync(restore, "restoreLyricsRevision");
        using var notStored = await PostAsync(ApiRoutes.LyricsRevisionRestore(sheet.Id, 99), new RevisionRequest { ExpectedRevision = 5 });
        using var staleRestore = await PostAsync(ApiRoutes.LyricsRevisionRestore(sheet.Id, 1), new RevisionRequest { ExpectedRevision = 1 });
        (notStored.StatusCode, await Code(staleRestore)).ShouldBe((HttpStatusCode.NotFound, "revision-conflict"));
    }

    [Fact]
    public async Task AcceptingAnAlternative_SupersedesItsGroupAndUndoReopensThem()
    {
        var group = Guid.NewGuid();
        var (sheet, ids) = await SeededAsync(
            Replace(3, 0, "the dog", "a dog") with { Type = SuggestionTypeDto.WriteAlternative, GroupId = group, Category = SuggestionCategoryDto.Alternative },
            Replace(3, 0, "the dog", "my dog") with { Type = SuggestionTypeDto.WriteAlternative, GroupId = group, Category = SuggestionCategoryDto.Alternative });

        using var accepted = await PostAsync(ApiRoutes.LyricsSuggestionsAccept(sheet.Id), new SuggestionDecisionRequest { SuggestionIds = [ids[1]], ExpectedRevision = 1 });
        using var all = await GetAsync(ApiRoutes.LyricsSuggestions(sheet.Id, SuggestionStatusDto.Superseded));
        using var undo = await PostAsync(ApiRoutes.LyricsUndo(sheet.Id), new RevisionRequest { ExpectedRevision = 2 });

        (await accepted.BodyAsync<AcceptResultDto>()).Text.ShouldEndWith("my dog");
        (await all.BodyAsync<SuggestionListDto>()).Suggestions.Single().SuggestionId.ShouldBe(ids[0]);
        (await undo.BodyAsync<AcceptResultDto>()).Reopened!.ShouldBe(ids, ignoreOrder: true);
    }

    [Fact]
    public async Task MarkIntentional_WordPhraseLineAndRules()
    {
        var (sheet, ids) = await SeededAsync(Replace(1, 4, "cat", "dog"), Replace(2, 0, "on the", "in a"), Replace(3, 4, "dog", "cat"), Note(1, 0, "the"));

        using var word = await MarkAsync(sheet.Id, ids[0], IntentionalScopeDto.Word);
        using var phrase = await MarkAsync(sheet.Id, ids[1], IntentionalScopeDto.Phrase);
        using var line = await MarkAsync(sheet.Id, ids[2], IntentionalScopeDto.Line);
        using var wordFromPhrase = await MarkAsync(sheet.Id, ids[1], IntentionalScopeDto.Word);
        using var licence = await MarkAsync(sheet.Id, ids[3], IntentionalScopeDto.Word);
        using var missing = await Client.PostAsJsonAsync(new Uri(ApiRoutes.LyricsSuggestionIntentional(sheet.Id, ids[0]), UriKind.Relative), (object?)null, Ct);

        (await word.BodyAsync<IntentionalMarkDto>()).ShouldSatisfyAllConditions(m => m.Text.ShouldBe("cat"), m => m.InText.ShouldBeTrue());
        (await phrase.BodyAsync<IntentionalMarkDto>()).Text.ShouldBe("on the");
        (await line.BodyAsync<IntentionalMarkDto>()).ShouldSatisfyAllConditions(m => m.Scope.ShouldBe(IntentionalScopeDto.Line), m => m.Text.ShouldBe("the dog"));
        (await Code(wordFromPhrase), await Code(licence), missing.StatusCode).ShouldBe(("suggestion-not-open", "suggestion-not-open", HttpStatusCode.BadRequest));
        await ContractAssert.MatchesAsync(word, "markSuggestionIntentional");
        using var marks = await GetAsync(ApiRoutes.LyricsIntentionalMarks(sheet.Id));
        (await marks.BodyAsync<List<IntentionalMarkDto>>()).Count.ShouldBe(3);
        await ContractAssert.MatchesAsync(marks, "listIntentionalMarks");
    }

    [Fact]
    public async Task MarksCrudAndLimit()
    {
        var sheet = await SheetAsync(Text);

        using var created = await PostAsync(ApiRoutes.LyricsIntentionalMarks(sheet.Id), new IntentionalMarkInput { Scope = IntentionalScopeDto.Phrase, Text = "  the  mat ", Note = "mine" });
        using var same = await PostAsync(ApiRoutes.LyricsIntentionalMarks(sheet.Id), new IntentionalMarkInput { Scope = IntentionalScopeDto.Phrase, Text = "the mat" });
        using var invalid = await PostAsync(ApiRoutes.LyricsIntentionalMarks(sheet.Id), new IntentionalMarkInput { Scope = IntentionalScopeDto.Word, Text = "two words" });
        using var missing = await PostAsync(ApiRoutes.LyricsIntentionalMarks(Guid.NewGuid()), new IntentionalMarkInput { Scope = IntentionalScopeDto.Word, Text = "x" });
        var mark = await created.BodyAsync<IntentionalMarkDto>();

        (mark.Text, (await same.BodyAsync<IntentionalMarkDto>()).MarkId).ShouldBe(("the mat", mark.MarkId));
        (await invalid.FieldErrorsAsync("text")).ShouldNotBeEmpty();
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(created, "createIntentionalMark");
        await ContractAssert.MatchesAsync(invalid, "createIntentionalMark");
        for (var i = 1; i < 200; i++)
        {
            (await PostAsync(ApiRoutes.LyricsIntentionalMarks(sheet.Id), new IntentionalMarkInput { Scope = IntentionalScopeDto.Word, Text = $"w{i}" })).Dispose();
        }

        using var limit = await PostAsync(ApiRoutes.LyricsIntentionalMarks(sheet.Id), new IntentionalMarkInput { Scope = IntentionalScopeDto.Word, Text = "last" });
        (limit.StatusCode, (await limit.JsonAsync()).GetProperty("type").GetString()).ShouldBe((HttpStatusCode.Conflict, "https://stemstudio.local/problems/mark-limit"));
        await ContractAssert.MatchesAsync(limit, "createIntentionalMark");
        using var deleted = await Client.DeleteAsync(new Uri(ApiRoutes.LyricsIntentionalMark(sheet.Id, mark.MarkId), UriKind.Relative), Ct);
        using var deletedAgain = await Client.DeleteAsync(new Uri(ApiRoutes.LyricsIntentionalMark(sheet.Id, mark.MarkId), UriKind.Relative), Ct);
        (deleted.StatusCode, deletedAgain.StatusCode).ShouldBe((HttpStatusCode.NoContent, HttpStatusCode.NotFound));
        await ContractAssert.MatchesAsync(deleted, "deleteIntentionalMark");
    }

    [Fact]
    public async Task MarkedTextAndPersonalWords_AreRespectedByTheNextCheck()
    {
        var sheet = await SheetAsync("the Zelmara nigth\nthe cat");
        (await PostAsync(ApiRoutes.LyricsIntentionalMarks(sheet.Id), new IntentionalMarkInput { Scope = IntentionalScopeDto.Word, Text = "nigth" })).Dispose();
        using var word = await PostAsync(ApiRoutes.LyricAssistWords, new LyricWordInput { Language = AssistLanguageDto.En, Word = "zelmara" });

        var assist = await RunToEndAsync(sheet.Id, Check());

        word.StatusCode.ShouldBe(HttpStatusCode.Created);
        assist.Suggestions.ShouldBeEmpty();
        assist.Discarded.Filtered.ShouldBe(1);
    }

    [Fact]
    public async Task Words_CrudLimitsAndLanguage()
    {
        using var added = await PostAsync(ApiRoutes.LyricAssistWords, new LyricWordInput { Language = AssistLanguageDto.Nl, Word = " Zelmara " });
        using var again = await PostAsync(ApiRoutes.LyricAssistWords, new LyricWordInput { Language = AssistLanguageDto.Nl, Word = "Zelmara" });
        using var invalid = await PostAsync(ApiRoutes.LyricAssistWords, new LyricWordInput { Language = AssistLanguageDto.Nl, Word = "two words" });
        using var list = await GetAsync(ApiRoutes.LyricAssistWordsFor(AssistLanguageDto.Nl));
        using var english = await GetAsync(ApiRoutes.LyricAssistWordsFor(AssistLanguageDto.En));
        using var noLanguage = await GetAsync(ApiRoutes.LyricAssistWords);
        var word = await added.BodyAsync<LyricWordDto>();

        ((await again.BodyAsync<LyricWordDto>()).WordId, word.Word).ShouldBe((word.WordId, "Zelmara"));
        (await invalid.FieldErrorsAsync("word")).ShouldNotBeEmpty();
        (await list.BodyAsync<List<LyricWordDto>>()).Single().WordId.ShouldBe(word.WordId);
        (await english.BodyAsync<List<LyricWordDto>>()).ShouldBeEmpty();
        (await noLanguage.FieldErrorsAsync("language")).ShouldNotBeEmpty();
        await ContractAssert.MatchesAsync(added, "addLyricWord");
        await ContractAssert.MatchesAsync(list, "listLyricWords");
        using var deleted = await Client.DeleteAsync(new Uri(ApiRoutes.LyricAssistWord(word.WordId), UriKind.Relative), Ct);
        using var deletedAgain = await Client.DeleteAsync(new Uri(ApiRoutes.LyricAssistWord(word.WordId), UriKind.Relative), Ct);
        (deleted.StatusCode, deletedAgain.StatusCode).ShouldBe((HttpStatusCode.NoContent, HttpStatusCode.NotFound));
        await ContractAssert.MatchesAsync(deletedAgain, "deleteLyricWord");
    }

    [Fact]
    public async Task SheetSettings_SensitivityAndRhythmTarget()
    {
        var sheet = await SheetAsync(Text);
        var target = new RhythmTargetDto { Bpm = 92, BeatsPerBar = 4, BarsPerLine = 2, SyllablesPerBeat = 1.5 };

        using var set = await Client.PutAsJsonAsync(new Uri(ApiRoutes.LyricsAssistSettings(sheet.Id), UriKind.Relative), new SheetAssistSettingsUpdate { Sensitivity = SensitivityDto.Strict, RhythmTarget = target }, Ct);
        using var keep = await Client.PutAsync(new Uri(ApiRoutes.LyricsAssistSettings(sheet.Id), UriKind.Relative), JsonContent.Create(new { sensitivity = "loose" }), Ct);
        using var clear = await Client.PutAsync(new Uri(ApiRoutes.LyricsAssistSettings(sheet.Id), UriKind.Relative), JsonContent.Create(new { rhythmTarget = (object?)null }), Ct);
        using var invalid = await Client.PutAsJsonAsync(new Uri(ApiRoutes.LyricsAssistSettings(sheet.Id), UriKind.Relative), new SheetAssistSettingsUpdate { RhythmTarget = target with { Bpm = 300 } }, Ct);
        using var missing = await Client.PutAsJsonAsync(new Uri(ApiRoutes.LyricsAssistSettings(Guid.NewGuid()), UriKind.Relative), new SheetAssistSettingsUpdate { Sensitivity = SensitivityDto.Strict }, Ct);

        (await set.BodyAsync<SheetAssistInfoDto>()).ShouldSatisfyAllConditions(i => i.Sensitivity.ShouldBe(SensitivityDto.Strict), i => i.RhythmTarget.ShouldBe(target));
        (await keep.BodyAsync<SheetAssistInfoDto>()).ShouldSatisfyAllConditions(i => i.Sensitivity.ShouldBe(SensitivityDto.Loose), i => i.RhythmTarget.ShouldBe(target));
        (await clear.BodyAsync<SheetAssistInfoDto>()).RhythmTarget.ShouldBeNull();
        (await invalid.FieldErrorsAsync("rhythmTarget.bpm")).ShouldNotBeEmpty();
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(set, "updateSheetAssistSettings");
        await ContractAssert.MatchesAsync(invalid, "updateSheetAssistSettings");
        using var sheetNow = await GetAsync(ApiRoutes.LyricsSheet(sheet.Id));
        (await sheetNow.BodyAsync<LyricsSheetDto>()).Revision.ShouldBe(1);
    }

    private static AssistantSuggestion Replace(int line, int start, string original, string proposal) => new()
    {
        Category = SuggestionCategoryDto.Spelling,
        Type = SuggestionTypeDto.SpellingTypo, Severity = SuggestionSeverityDto.Error, Anchor = new AnchorDto { Line = line, Start = start, Length = original.Length, Original = original },
        Operation = SuggestionOperationDto.Replace, Proposal = proposal, Explanation = "Better.",
    };

    private static AssistantSuggestion Note(int line, int start, string original) => Replace(line, start, original, "x") with
    {
        Category = SuggestionCategoryDto.Licence, Type = SuggestionTypeDto.LicenceSlang, Severity = SuggestionSeverityDto.Idea, Operation = SuggestionOperationDto.None, Proposal = null,
    };

    private Task<(LyricsSheetDto Sheet, IReadOnlyList<Guid> Ids)> SeededAsync(params AssistantSuggestion[] suggestions) => SeededAsync(Text, suggestions);

    private async Task<(LyricsSheetDto Sheet, IReadOnlyList<Guid> Ids)> SeededAsync(string text, params AssistantSuggestion[] suggestions)
    {
        Assistants.Assistant = new ScriptedAssistant((_, sink, ct) => sink.BlockCompletedAsync(new LyricBlockResult { BlockIndex = 0, Suggestions = suggestions }, ct));
        var sheet = await SheetAsync(text);
        (await Client.PutAsJsonAsync(new Uri(ApiRoutes.LyricsAssistSettings(sheet.Id), UriKind.Relative), new SheetAssistSettingsUpdate { Sensitivity = SensitivityDto.Strict }, Ct)).Dispose();
        var assist = await RunToEndAsync(sheet.Id, Check());
        var byAnchor = suggestions.Select(s => assist.Suggestions.First(x => x.Anchor == s.Anchor && x.Proposal == s.Proposal).SuggestionId).ToList();
        return (sheet, byAnchor);
    }

    private Task<HttpResponseMessage> GetAsync(string route) => Client.GetAsync(new Uri(route, UriKind.Relative), Ct);

    private Task<HttpResponseMessage> PostAsync<T>(string route, T body) => Client.PostAsJsonAsync(new Uri(route, UriKind.Relative), body, Ct);

    private Task<HttpResponseMessage> MarkAsync(Guid sheetId, Guid suggestionId, IntentionalScopeDto scope) =>
        PostAsync(ApiRoutes.LyricsSuggestionIntentional(sheetId, suggestionId), new MarkSuggestionIntentionalRequest { Scope = scope, ExpectedRevision = 1 });

    private async Task PutSheetAsync(Guid id, string text) =>
        (await Client.PutAsJsonAsync(new Uri(ApiRoutes.LyricsSheet(id), UriKind.Relative), new LyricsInput { Title = "Song", Language = "en", Text = text }, Ct)).Dispose();

    private static async Task<string?> Code(HttpResponseMessage response) => (await response.JsonAsync()).GetProperty("code").GetString();
}

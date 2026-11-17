using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Lyrics.Assist;
using StemStudio.Web.Tests.Fakes;
using static StemStudio.Web.Tests.Fakes.LyricAssistData;

namespace StemStudio.Web.Tests.Songs;

/// <summary>Decisions and sheet settings of the assistant session (023 US1, US3, US6; FR-006 - FR-012, FR-031).</summary>
public sealed class LyricAssistDecisionTests
{
    private const string Fixed = "[verse]\nIk weet niet waar de wind gaat\nDe nacht zijn warm en stil";
    private readonly IStemStudioApiClient _api = Substitute.For<IStemStudioApiClient>();
    private readonly LyricAssistSession _session;

    public LyricAssistDecisionTests()
    {
        _session = new LyricAssistSession(_api, new EditorDocument(Sheet()));
        _session.Board.Merge([SpellingFix(), GrammarFix(), DeviceIdea()]);
        _api.ListLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionStatusDto?>(), Arg.Any<CancellationToken>())
            .Returns(List(SpellingFix(SuggestionStatusDto.Accepted), GrammarFix(), DeviceIdea()));
        _api.AcceptLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionDecisionRequest>(), Arg.Any<CancellationToken>()).Returns(Accepted(Fixed));
    }

    [Fact]
    public async Task Accept_TakesTheNewRevision_AndRefreshesTheList()
    {
        await _session.AcceptAsync([Spelling]);

        await _api.Received(1).AcceptLyricSuggestionsAsync(LyricsId, Arg.Is<SuggestionDecisionRequest>(r => r.ExpectedRevision == 3 && r.SuggestionIds.Single() == Spelling), Arg.Any<CancellationToken>());
        (_session.Document.Text, _session.Document.Revision, _session.Document.Assist.CanUndo, _session.Document.IsDirty).ShouldBe((Fixed, 4, true, false));
        _session.Board.Open.Select(s => s.SuggestionId).ShouldBe([Grammar, Device]);
        _session.Live.ShouldBe("Suggestion accepted");
    }

    [Fact]
    public async Task Accept_ReportsStaleSuggestions()
    {
        _api.AcceptLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionDecisionRequest>(), Arg.Any<CancellationToken>()).Returns(Accepted(Fixed, applied: [Spelling, Grammar], stale: [Device]));

        await _session.AcceptAsync([Spelling, Grammar, Device]);

        _session.Live.ShouldBe("2 suggestions accepted; 1 outdated, not applied");
    }

    [Fact]
    public async Task Accept_Nothing_SendsNothing()
    {
        await _session.AcceptAsync([]);

        _api.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task AcceptAll_TakesTheFilteredReplacements()
    {
        _session.Board.Toggle(SuggestionCategoryDto.Spelling);
        _session.Board.Toggle(SuggestionCategoryDto.Grammar);

        await _session.AcceptAllAsync();

        await _api.Received(1).AcceptLyricSuggestionsAsync(LyricsId, Arg.Is<SuggestionDecisionRequest>(r => r.SuggestionIds.SequenceEqual(new[] { Spelling, Grammar })), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevisionConflict_ReloadsTheSheet_AndTheSuggestions()
    {
        _api.AcceptLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionDecisionRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(Problem(LyricAssistProblemTypes.RevisionConflict, """{"currentRevision":5}"""));
        _api.GetLyricsSheetAsync(LyricsId, Arg.Any<CancellationToken>()).Returns(Sheet(text: Fixed, revision: 5));

        await _session.AcceptAsync([Grammar]);

        (_session.Document.Revision, _session.Document.Text).ShouldBe((5, Fixed));
        _session.Error.ShouldStartWith("The lyrics were changed elsewhere");
        await _api.Received(1).ListLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionStatusDto?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TooLong_IsShown_AndTheSuggestionStaysOpen()
    {
        _api.AcceptLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionDecisionRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(LyricAssistProblemTypes.TooLong, status: System.Net.HttpStatusCode.UnprocessableEntity));

        await _session.AcceptAsync([Grammar]);

        (_session.Error, _session.Refusal).ShouldBe(("Problem too-long", null));
        _session.Board.Find(Grammar)!.Status.ShouldBe(SuggestionStatusDto.Open);
    }

    [Fact]
    public async Task Accept_WithUnsavedChanges_SavesFirst()
    {
        _session.Document.Title = "Zomeravond 2";
        _api.UpdateLyricsSheetAsync(LyricsId, Arg.Any<LyricsInput>(), Arg.Any<CancellationToken>()).Returns(Sheet(revision: 4) with { Title = "Zomeravond 2" });

        await _session.AcceptAsync([Spelling]);

        await _api.Received(1).AcceptLyricSuggestionsAsync(LyricsId, Arg.Is<SuggestionDecisionRequest>(r => r.ExpectedRevision == 4), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reject_MergesTheAnswer()
    {
        _api.RejectLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionDecisionRequest>(), Arg.Any<CancellationToken>()).Returns(List(GrammarFix() with { Status = SuggestionStatusDto.Rejected }));
        _api.ListLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionStatusDto?>(), Arg.Any<CancellationToken>()).Returns(List());

        await _session.RejectAsync(Grammar);

        _session.Board.Find(Grammar)!.Status.ShouldBe(SuggestionStatusDto.Rejected);
        _session.Live.ShouldBe("Suggestion rejected");
    }

    [Fact]
    public async Task MarkIntentional_SendsTheScope()
    {
        _api.MarkSuggestionIntentionalAsync(LyricsId, Spelling, Arg.Any<MarkSuggestionIntentionalRequest>(), Arg.Any<CancellationToken>())
            .Returns(new IntentionalMarkDto { MarkId = Guid.NewGuid(), Scope = IntentionalScopeDto.Word, Text = "wiet", InText = true, CreatedAt = DateTimeOffset.UnixEpoch });

        await _session.MarkIntentionalAsync(Spelling, IntentionalScopeDto.Line);

        await _api.Received(1).MarkSuggestionIntentionalAsync(LyricsId, Spelling, Arg.Is<MarkSuggestionIntentionalRequest>(r => r.Scope == IntentionalScopeDto.Line && r.ExpectedRevision == 3), Arg.Any<CancellationToken>());
        _session.Live.ShouldBe("Marked as intentional: wiet");
    }

    [Theory]
    [InlineData(SuggestionCategoryDto.Spelling, IntentionalScopeDto.Word, true)]
    [InlineData(SuggestionCategoryDto.Grammar, IntentionalScopeDto.Phrase, true)]
    [InlineData(SuggestionCategoryDto.Flow, IntentionalScopeDto.Phrase, true)]
    [InlineData(SuggestionCategoryDto.Device, IntentionalScopeDto.Phrase, false)]
    public void DefaultScope_AndCorrections(SuggestionCategoryDto category, IntentionalScopeDto scope, bool correction)
    {
        var s = GrammarFix() with { Category = category };

        (LyricAssistSession.DefaultScope(s), LyricAssistSession.IsCorrection(s)).ShouldBe((scope, correction));
    }

    [Fact]
    public async Task AddWord_StoresTheWord_AndClosesTheSuggestion()
    {
        _api.RejectLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionDecisionRequest>(), Arg.Any<CancellationToken>()).Returns(List(SpellingFix(SuggestionStatusDto.Rejected)));

        await _session.AddWordAsync(Spelling);

        await _api.Received(1).AddLyricWordAsync(Arg.Is<LyricWordInput>(w => w.Word == "wiet" && w.Language == AssistLanguageDto.Nl), Arg.Any<CancellationToken>());
        _session.Live.ShouldBe("Added to my words: wiet");
    }

    [Fact]
    public async Task AddWord_ForAnUnknownSuggestion_DoesNothing()
    {
        await _session.AddWordAsync(Guid.NewGuid());

        await _api.DidNotReceive().AddLyricWordAsync(Arg.Any<LyricWordInput>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Undo_RestoresTheText()
    {
        _api.UndoLyricSuggestionsAsync(LyricsId, 3, Arg.Any<CancellationToken>()).Returns(Accepted(Text, revision: 5));

        await _session.UndoAsync();

        (_session.Document.Text, _session.Document.Revision, _session.Live).ShouldBe((Text, 5, "Undone: the text before the last accept is back"));
    }

    [Fact]
    public async Task Undo_BlockedByAnEdit_SaysSo()
    {
        _api.UndoLyricSuggestionsAsync(LyricsId, 3, Arg.Any<CancellationToken>()).ThrowsAsync(Problem(LyricAssistProblemTypes.UndoBlockedByEdit, """{"changedLines":[2]}"""));

        await _session.UndoAsync();

        _session.Error.ShouldBe("Problem undo-blocked-by-edit");
    }

    [Fact]
    public async Task Sensitivity_KeepsTheRhythmTarget()
    {
        var target = new RhythmTargetDto { Bpm = 92, BeatsPerBar = 4, BarsPerLine = 2, SyllablesPerBeat = 1.5 };
        var session = new LyricAssistSession(_api, new EditorDocument(Sheet(assist: SheetAssistInfoDto.Default with { RhythmTarget = target })));
        _api.UpdateSheetAssistSettingsAsync(LyricsId, Arg.Any<SheetAssistSettingsUpdate>(), Arg.Any<CancellationToken>())
            .Returns(SheetAssistInfoDto.Default with { Sensitivity = SensitivityDto.Strict, RhythmTarget = target });

        await session.SetSensitivityAsync(SensitivityDto.Strict);

        await _api.Received(1).UpdateSheetAssistSettingsAsync(LyricsId, Arg.Is<SheetAssistSettingsUpdate>(u => u.Sensitivity == SensitivityDto.Strict && u.RhythmTarget == target && u.RhythmTargetSpecified), Arg.Any<CancellationToken>());
        session.Document.Assist.Sensitivity.ShouldBe(SensitivityDto.Strict);
    }

    [Fact]
    public async Task RhythmTarget_ClearSendsNull()
    {
        _api.UpdateSheetAssistSettingsAsync(LyricsId, Arg.Any<SheetAssistSettingsUpdate>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ApiException(ApiError.Unreachable));

        await _session.SetRhythmTargetAsync(null);

        await _api.Received(1).UpdateSheetAssistSettingsAsync(LyricsId, Arg.Is<SheetAssistSettingsUpdate>(u => u.RhythmTarget == null && u.RhythmTargetSpecified && u.Sensitivity == null), Arg.Any<CancellationToken>());
        _session.Error.ShouldNotBeNull();
    }

    [Fact]
    public async Task OwnWork_ConfirmsTheMatchedTrack_ThenRevokes()
    {
        await RefuseAsNotOwnAsync(overridable: true);
        var confirmed = new OwnWorkOverrideDto { StatementVersion = "own-work.v1", ConfirmedBy = "Robin", ConfirmedAt = DateTimeOffset.UnixEpoch, MatchedTrackId = TrackId, MatchedTrackTitle = "Fake Song" };
        _api.ConfirmOwnWorkAsync(LyricsId, Arg.Any<OwnWorkConfirmRequest>(), Arg.Any<CancellationToken>()).Returns(confirmed);

        (await _session.ConfirmOwnWorkAsync("  Robin ", "own-work.v1")).ShouldBeTrue();

        await _api.Received(1).ConfirmOwnWorkAsync(LyricsId, Arg.Is<OwnWorkConfirmRequest>(r => r.MatchedTrackId == TrackId && r.ConfirmedBy == "Robin" && r.Confirm && r.StatementVersion == "own-work.v1"), Arg.Any<CancellationToken>());
        (_session.Refusal, _session.Document.Assist.OwnWorkOverride).ShouldBe((null, confirmed));

        await _session.RevokeOwnWorkAsync();

        await _api.Received(1).RevokeOwnWorkAsync(LyricsId, Arg.Any<CancellationToken>());
        _session.Document.Assist.OwnWorkOverride.ShouldBeNull();
    }

    [Fact]
    public async Task OwnWork_NotOverridable_SendsNothing()
    {
        await RefuseAsNotOwnAsync(overridable: false);

        (await _session.ConfirmOwnWorkAsync("Robin", "own-work.v1")).ShouldBeFalse();

        await _api.DidNotReceive().ConfirmOwnWorkAsync(Arg.Any<Guid>(), Arg.Any<OwnWorkConfirmRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OwnWork_RejectedByTheServer_KeepsTheRefusal()
    {
        await RefuseAsNotOwnAsync(overridable: true);
        _api.ConfirmOwnWorkAsync(LyricsId, Arg.Any<OwnWorkConfirmRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(Problem(LyricAssistProblemTypes.StatementOutdated));

        (await _session.ConfirmOwnWorkAsync("Robin", "own-work.v0")).ShouldBeFalse();

        (_session.Refusal!.IsNotOwn, _session.Error).ShouldBe((true, "Problem statement-outdated"));
    }

    [Fact]
    public async Task Revoke_Failing_KeepsTheOverride()
    {
        var info = SheetAssistInfoDto.Default with { OwnWorkOverride = new OwnWorkOverrideDto { StatementVersion = "own-work.v1", ConfirmedBy = "R", ConfirmedAt = DateTimeOffset.UnixEpoch, MatchedTrackId = TrackId, MatchedTrackTitle = "T" } };
        var session = new LyricAssistSession(_api, new EditorDocument(Sheet(assist: info)));
        _api.RevokeOwnWorkAsync(LyricsId, Arg.Any<CancellationToken>()).ThrowsAsync(new ApiException(ApiError.Unreachable));

        await session.RevokeOwnWorkAsync();

        session.Document.Assist.OwnWorkOverride.ShouldNotBeNull();
    }

    [Fact]
    public async Task Settings_AreReadWithAGet()
    {
        _api.GetLyricAssistSettingsAsync(Arg.Any<CancellationToken>()).Returns(LyricAssistData.Settings());

        (await _session.LoadSettingsAsync())!.OwnWorkStatementText.ShouldBe("I confirm that I wrote this text myself.");

        await _api.DidNotReceive().UpdateLyricAssistSettingsAsync(Arg.Any<LyricAssistSettingsUpdate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LanguageGuess_SetsAndSavesTheLanguage()
    {
        var session = new LyricAssistSession(_api, new EditorDocument(Sheet("unknown")));
        _api.GuessLyricsLanguageAsync(Text, Arg.Any<CancellationToken>()).Returns(new LanguageGuessDto { Language = AssistLanguageDto.Nl, Confidence = 0.9 });
        _api.UpdateLyricsSheetAsync(LyricsId, Arg.Any<LyricsInput>(), Arg.Any<CancellationToken>()).Returns(Sheet("nl", revision: 4));

        await session.GuessLanguageAsync();
        session.Guess!.Language.ShouldBe(AssistLanguageDto.Nl);
        await session.ApplyGuessAsync();

        await _api.Received(1).UpdateLyricsSheetAsync(LyricsId, Arg.Is<LyricsInput>(i => i.Language == "nl"), Arg.Any<CancellationToken>());
        (session.Guess, session.Document.AssistLanguage).ShouldBe((null, AssistLanguageDto.Nl));
    }

    [Fact]
    public async Task LanguageGuess_OnlyForUnknownSheets()
    {
        await _session.GuessLanguageAsync();
        await _session.ApplyGuessAsync();

        await _api.DidNotReceive().GuessLyricsLanguageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("wiet", false, IntentionalScopeDto.Word)]
    [InlineData("Gij zijt", false, IntentionalScopeDto.Phrase)]
    [InlineData("Ik wiet niet waar de wind gaat", true, IntentionalScopeDto.Line)]
    public async Task MarkSelection_PicksTheScope(string text, bool line, IntentionalScopeDto scope)
    {
        _api.ListIntentionalMarksAsync(LyricsId, Arg.Any<CancellationToken>()).Returns([new IntentionalMarkDto { MarkId = Guid.Empty, Scope = scope, Text = text, InText = true, CreatedAt = DateTimeOffset.UnixEpoch }]);

        await _session.MarkSelectionAsync($" {text} ", line);

        await _api.Received(1).CreateIntentionalMarkAsync(LyricsId, Arg.Is<IntentionalMarkInput>(m => m.Scope == scope && m.Text == text), Arg.Any<CancellationToken>());
        _session.Marks.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task MarkSelection_Empty_SendsNothing()
    {
        await _session.MarkSelectionAsync("  ", false);

        _api.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task MarksWordsHistoryAndRevisions_Load_AndDelete()
    {
        _api.ListLyricWordsAsync(AssistLanguageDto.Nl, Arg.Any<CancellationToken>()).Returns([new LyricWordDto { WordId = Guid.Empty, Language = AssistLanguageDto.Nl, Word = "Zeeland", CreatedAt = DateTimeOffset.UnixEpoch }]);
        _api.ListLyricAssistsAsync(LyricsId, Arg.Any<CancellationToken>()).Returns([Assist(AssistStatusDto.Completed)]);
        _api.ListLyricsRevisionsAsync(LyricsId, Arg.Any<CancellationToken>()).Returns([new LyricsRevisionSummaryDto { Revision = 2, Cause = RevisionCauseDto.Accept, CreatedAt = DateTimeOffset.UnixEpoch }]);
        _api.ListIntentionalMarksAsync(LyricsId, Arg.Any<CancellationToken>()).ThrowsAsync(new ApiException(ApiError.Unreachable));
        _api.RestoreLyricsRevisionAsync(LyricsId, 2, 3, Arg.Any<CancellationToken>()).Returns(Accepted(Text, revision: 4));

        await _session.DeleteWordAsync(Guid.Empty);
        await _session.LoadHistoryAsync();
        await _session.DeleteMarkAsync(Guid.Empty);
        (_session.Marks.Count, _session.Error).ShouldBe((0, ApiError.Unreachable.UserMessage));
        await _session.RestoreAsync(2);

        (_session.Words.Single().Word, _session.History.Count, _session.Revisions.Single().Revision, _session.Document.Revision).ShouldBe(("Zeeland", 1, 2, 4));
        _session.Live.ShouldBe("Revision 2 restored");
        await _api.Received(1).DeleteLyricWordAsync(Guid.Empty, Arg.Any<CancellationToken>());
        await _api.Received(1).DeleteIntentionalMarkAsync(LyricsId, Guid.Empty, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Words_NeedASupportedLanguage()
    {
        var session = new LyricAssistSession(_api, new EditorDocument(Sheet("de")));

        await session.LoadWordsAsync();

        await _api.DidNotReceive().ListLyricWordsAsync(Arg.Any<AssistLanguageDto>(), Arg.Any<CancellationToken>());
    }

    private async Task RefuseAsNotOwnAsync(bool overridable)
    {
        _api.CreateLyricAssistAsync(LyricsId, Arg.Any<CreateLyricAssistRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(NotOwn(overridable));
        await _session.StartAsync(new AssistRequest(AssistKindDto.Check));
        _api.ClearReceivedCalls();
    }
}

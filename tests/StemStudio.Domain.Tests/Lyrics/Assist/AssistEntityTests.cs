using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Domain.Tests.Lyrics.Assist;

/// <summary>023 T012/T014: the sheet's 023 members, assists, suggestions and the small records (data-model 2-7).</summary>
public sealed class AssistEntityTests
{
    private static readonly DateTime Now = AssistFixtures.Now;

    [Fact]
    public void Sheet_RevisionCountsEverySaveAndPrunesAssistedLines()
    {
        var sheet = AssistFixtures.NewSheet();
        (sheet.Revision, sheet.LastCause, sheet.Sensitivity, sheet.CanUndo).ShouldBe((1, RevisionCause.Created, Sensitivity.Balanced, false));

        var id = Guid.NewGuid();
        sheet.ApplyAccepted(SuggestionApplier.Apply(sheet.Title, sheet.Text, [new ApplicableSuggestion(id, new Anchor(1, 0, 4, "line"), SuggestionOperation.Replace, "row")]), Now);
        (sheet.Revision, sheet.CanUndo, sheet.Text).ShouldBe((2, true, "row one\nline two"));
        sheet.AssistedLineNumbers().ShouldBe([(1, id)]);

        sheet.Update(new LyricsContent("Song", "en", "row one!\nline two"), Now);
        (sheet.Revision, sheet.LastCause, sheet.AssistedLines.Count).ShouldBe((3, RevisionCause.UserSave, 0));
    }

    [Fact]
    public void Sheet_RestoreRemovesTheGroupsAssistedLines()
    {
        var sheet = AssistFixtures.NewSheet();
        var id = Guid.NewGuid();
        sheet.ApplyAccepted(SuggestionApplier.Apply(sheet.Title, sheet.Text, [new ApplicableSuggestion(id, new Anchor(2, 8, 0, ""), SuggestionOperation.InsertAfter, "line one")]), Now);
        sheet.AssistedLines.Count.ShouldBe(1);

        sheet.Restore("Old", "line one\nline two", [id], Now);

        (sheet.Title, sheet.Revision, sheet.LastCause, sheet.AssistedLines.Count).ShouldBe(("Old", 3, RevisionCause.Undo, 0));
    }

    [Fact]
    public void Sheet_AssistedLinesAreCappedAt400()
    {
        var sheet = AssistFixtures.NewSheet(string.Join('\n', Enumerable.Range(0, 450).Select(i => $"l{i}")));
        var suggestions = Enumerable.Range(0, 450).Select(i => new ApplicableSuggestion(Guid.NewGuid(), new Anchor(i + 1, 0, 1, "l"), SuggestionOperation.Replace, "m")).ToList();

        sheet.ApplyAccepted(SuggestionApplier.Apply(sheet.Title, sheet.Text, suggestions), Now);

        sheet.AssistedLines.Count.ShouldBe(400);
    }

    [Fact]
    public void Sheet_SettingsMarksAndOwnWork()
    {
        var sheet = AssistFixtures.NewSheet();
        var target = new RhythmTarget(92, 4, 2, 1.5);
        sheet.SetAssistSettings(Sensitivity.Strict, true, target);
        sheet.SetAssistSettings(null, false, null);
        (sheet.Sensitivity, sheet.RhythmTarget).ShouldBe((Sensitivity.Strict, target));
        sheet.SetAssistSettings(null, true, null);
        sheet.RhythmTarget.ShouldBeNull();

        var mark = IntentionalMark.Create(Guid.NewGuid(), new MarkContent(IntentionalScope.Word, "line", null), Now);
        sheet.AddMark(mark).ShouldBe(mark);
        sheet.AddMark(IntentionalMark.Create(Guid.NewGuid(), new MarkContent(IntentionalScope.Word, "line", "again"), Now)).ShouldBe(mark);
        (sheet.RemoveMark(mark.Id), sheet.RemoveMark(mark.Id)).ShouldBe((true, false));

        var own = OwnWorkOverride.Create(" Robin ", new CorpusTrack(Guid.NewGuid(), "Paper Lanterns"), Now);
        sheet.ConfirmOwnWork(own);
        sheet.OwnWorkOverride.ShouldBe(own);
        (sheet.RevokeOwnWork(), sheet.RevokeOwnWork()).ShouldBe((true, false));
        sheet.Revision.ShouldBe(1);
        sheet.AssistLanguage.ShouldBe(AssistLanguage.English);
    }

    [Fact]
    public void Sheet_RefusesMarksOver200()
    {
        var sheet = AssistFixtures.NewSheet();
        for (var i = 0; i < IntentionalMark.MaxPerSheet; i++)
        {
            sheet.AddMark(IntentionalMark.Create(Guid.NewGuid(), new MarkContent(IntentionalScope.Word, $"w{i}", null), Now)).ShouldNotBeNull();
        }

        sheet.AddMark(IntentionalMark.Create(Guid.NewGuid(), new MarkContent(IntentionalScope.Word, "last", null), Now)).ShouldBeNull();
    }

    [Fact]
    public void OwnWork_ValidatesTheNameAndHashesTheStatement()
    {
        Should.Throw<DomainValidationException>(() => OwnWorkOverride.Create(" ", new CorpusTrack(Guid.NewGuid(), "T"), Now));
        Should.Throw<DomainValidationException>(() => OwnWorkOverride.Create(new string('n', 81), new CorpusTrack(Guid.NewGuid(), "T"), Now));
        var own = OwnWorkOverride.Create("Robin", new CorpusTrack(Guid.NewGuid(), new string('t', 300)), Now);
        (own.StatementVersion, own.StatementSha256.Length, own.MatchedTrackTitle.Length).ShouldBe(("own-work.v1", 64, 200));
    }

    [Fact]
    public void RhythmTarget_MathAndValidation()
    {
        var target = new RhythmTarget(92, 4, 2, 1.5);
        (target.SyllablesPerLine, Math.Round(target.SecondsPerLine, 3)).ShouldBe((12, 5.217));
        target.Validate().ShouldBeEmpty();
        new RhythmTarget(10, 1, 9, 0.3).Validate().Select(e => e.Field).ShouldBe(["rhythmTarget.bpm", "rhythmTarget.beatsPerBar", "rhythmTarget.barsPerLine", "rhythmTarget.syllablesPerBeat"]);
        new RhythmTarget(90, 4, 2, 1.3).Validate().Single().Field.ShouldBe("rhythmTarget.syllablesPerBeat");
    }

    [Fact]
    public void Words_AreNormalisedAndValidated()
    {
        LyricWord.Create(Guid.NewGuid(), AssistLanguage.Dutch, " Zelmara ", Now).Word.ShouldBe("Zelmara");
        Should.Throw<DomainValidationException>(() => LyricWord.Create(Guid.NewGuid(), AssistLanguage.Dutch, "two words", Now));
        Should.Throw<DomainValidationException>(() => LyricWord.Create(Guid.NewGuid(), AssistLanguage.Dutch, new string('a', 41), Now));
    }

    [Fact]
    public void Settings_ClaudeNoticeIsVersioned()
    {
        var settings = new LyricAssistSettings();
        settings.HasAcknowledged("v1").ShouldBeFalse();
        settings.SetClaudeNotice(true, "v1", Now);
        (settings.Id, settings.HasAcknowledged("v1"), settings.HasAcknowledged("v2")).ShouldBe((1, true, false));
        settings.SetClaudeNotice(false, "v1", Now);
        (settings.ClaudeNoticeAcknowledgedAt, settings.ClaudeNoticeVersion).ShouldBe((null, null));
    }

    [Fact]
    public void Assist_Lifecycle()
    {
        var sheet = AssistFixtures.NewSheet(language: "nl");
        var assist = AssistRun.Create(Guid.NewGuid(), sheet, new AssistRequest { Kind = AssistKind.Check, Categories = [SuggestionCategory.Spelling] }, Now);
        (assist.Status, assist.SheetRevision, assist.Language, assist.DurationMs(Now)).ShouldBe((AssistStatus.Queued, 1, AssistLanguage.Dutch, 0));

        assist.Start([new AssistLayer(SuggestionSource.Dictionary, "OpenTaal (nl)", true)], 2, Now);
        assist.UseModel("fake", "scripted", false, 3);
        assist.BlockDone(false, new DiscardedCounts { InvalidAnchor = 1 });
        assist.BlockDone(true, new DiscardedCounts());
        assist.CountShown(SuggestionCategory.Spelling);
        assist.CountDiscarded(new DiscardedCounts { Filtered = 2 });
        assist.RecordUsage(new AssistUsage(10, 5, 0));
        assist.Complete(Now.AddSeconds(2));

        (assist.Status, assist.BlocksDone, assist.BlocksSkipped, assist.Counts.Spelling, assist.Discarded.InvalidAnchor, assist.Discarded.Filtered)
            .ShouldBe((AssistStatus.Completed, 2, 1, 1, 1, 2));
        (assist.Backend, assist.Usage!.InputTokens, assist.DurationMs(Now)).ShouldBe(("fake", 10, 2000));
        assist.Status.IsTerminal().ShouldBeTrue();
    }

    [Fact]
    public void Assist_FailAndCancel()
    {
        var sheet = AssistFixtures.NewSheet();
        var failed = AssistRun.Create(Guid.NewGuid(), sheet, new AssistRequest { Kind = AssistKind.Titles }, Now);
        failed.Fail(AssistFailureCode.Timeout, new string('m', 600), "Try again", Now);
        (failed.Status, failed.FailureCode, failed.FailureMessage!.Length, failed.FailureFix).ShouldBe((AssistStatus.Failed, AssistFailureCode.Timeout, 500, "Try again"));
        failed.Fail(AssistFailureCode.Internal, " ", null, Now);
        failed.FailureMessage.ShouldBe("Internal");

        var cancelled = AssistRun.Create(Guid.NewGuid(), sheet, new AssistRequest { Kind = AssistKind.Check }, Now);
        cancelled.Cancel(Now);
        cancelled.Status.ShouldBe(AssistStatus.Cancelled);
        AssistStatus.Running.IsTerminal().ShouldBeFalse();
    }

    [Theory]
    [InlineData(SuggestionCategory.Spelling)]
    [InlineData(SuggestionCategory.Grammar)]
    [InlineData(SuggestionCategory.Flow)]
    [InlineData(SuggestionCategory.Licence)]
    [InlineData(SuggestionCategory.Device)]
    [InlineData(SuggestionCategory.Alternative)]
    [InlineData(SuggestionCategory.Continuation)]
    [InlineData(SuggestionCategory.Title)]
    public void Counts_AddPerCategory(SuggestionCategory category)
    {
        var counts = new CategoryCounts().Add(category);

        new[] { counts.Spelling, counts.Grammar, counts.Flow, counts.Licence, counts.Device, counts.Alternative, counts.Continuation, counts.Title }.Sum().ShouldBe(1);
    }

    [Fact]
    public void Types_HaveCategories()
    {
        Enum.GetValues<SuggestionType>().Select(SuggestionTypes.CategoryOf).Distinct().Count().ShouldBe(8);
        SuggestionTypes.CategoryOf(SuggestionType.GrammarVerbForm).ShouldBe(SuggestionCategory.Grammar);
        (SuggestionTypes.IsCorrection(SuggestionCategory.Flow), SuggestionTypes.IsCorrection(SuggestionCategory.Licence)).ShouldBe((true, false));
        (AssistLanguage.Dutch.Code(), AssistLanguage.English.Code(), AssistEnumExtensions.AssistLanguageOf("de")).ShouldBe(("nl", "en", null));
    }

    [Fact]
    public void Suggestion_LifecycleAndLimits()
    {
        var sheet = AssistFixtures.NewSheet();
        var assist = AssistRun.Create(Guid.NewGuid(), sheet, new AssistRequest { Kind = AssistKind.Check }, Now);
        var draft = new SuggestionDraft
        {
            Type = SuggestionType.SpellingTypo,
            Severity = SuggestionSeverity.Error,
            Anchor = new Anchor(1, 0, 4, "line"),
            Operation = SuggestionOperation.Replace,
            Proposal = new string('p', 500),
            Explanation = new string('e', 400),
            Source = SuggestionSource.Dictionary,
        };
        var suggestion = LyricSuggestion.Create(Guid.NewGuid(), assist, draft, Now);
        (suggestion.Category, suggestion.Proposal!.Length, suggestion.Explanation.Length, suggestion.IsOpen, suggestion.SheetId)
            .ShouldBe((SuggestionCategory.Spelling, 400, 300, true, sheet.Id));

        var group = Guid.NewGuid();
        suggestion.Decide(SuggestionStatus.Accepted, group, Now);
        (suggestion.Status, suggestion.AcceptGroupId).ShouldBe((SuggestionStatus.Accepted, group));
        suggestion.Reopen(new Anchor(2, 0, 4, "line"));
        (suggestion.IsOpen, suggestion.AcceptGroupId, suggestion.Anchor.Line).ShouldBe((true, null, 2));
        suggestion.Decide(SuggestionStatus.Rejected, group, Now);
        suggestion.AcceptGroupId.ShouldBeNull();
        suggestion.Reanchor(new Anchor(1, 0, 4, "line"));
        suggestion.Reanchor(null);
        suggestion.Status.ShouldBe(SuggestionStatus.Stale);
        suggestion.ToApplicable().Id.ShouldBe(suggestion.Id);
    }

    [Fact]
    public void Revision_And_Event_Snapshots()
    {
        var sheet = AssistFixtures.NewSheet();
        var revision = LyricsSheetRevision.Of(sheet, RevisionCause.Created, null, Now);
        (revision.SheetId, revision.Revision, revision.Text).ShouldBe((sheet.Id, 1, sheet.Text));
        var e = AssistEvent.Create(Guid.NewGuid(), 3, ("assist.progress", "{}"), Now);
        (e.Seq, e.Type, e.Data).ShouldBe((3, "assist.progress", "{}"));
    }
}

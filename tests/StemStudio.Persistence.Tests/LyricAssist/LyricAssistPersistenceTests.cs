using Microsoft.Data.Sqlite;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Lyrics.Assist;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence.Tests.LyricAssist;

/// <summary>023 T014: migration AddLyricAssist, revisions (retention 50), cascade delete, personal words, settings row (data-model 2-7).</summary>
public sealed class LyricAssistPersistenceTests
{
    private const string LastMigrationBefore023 = "20261116211814_AddMixPractice";
    private static readonly DateTime Now = new(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static LyricsSheet NewSheet() => LyricsSheet.Create(Guid.NewGuid(), new LyricsContent("Song", "nl", "regel een\nregel twee"), Now);

    [Fact]
    public async Task SheetFromBefore023_MigratesToRevisionOneAndBalanced()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stemstudio-023-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Pooling=False";
        try
        {
            await SqliteTestDatabase.MigrateLegacyAsync(connectionString, LastMigrationBefore023);
            var id = Guid.NewGuid();
            await ExecuteAsync(connectionString, $"INSERT INTO LyricsSheets (Id, Title, Language, Text, CreatedAt, UpdatedAt) VALUES ('{id.ToString().ToUpperInvariant()}', 'Oud', 'nl', 'tekst', '2026-11-14 12:00:00', '2026-11-14 12:00:00')");

            await using var db = await SqliteTestDatabase.CreateAsync(connectionString);
            await using var read = db.NewContext();
            var sheet = (await new LyricsSheetRepository(read).FindAsync(id, Ct))!;

            (sheet.Revision, sheet.Sensitivity, sheet.LastCause, sheet.RhythmTarget, sheet.OwnWorkOverride, sheet.IntentionalMarks.Count)
                .ShouldBe((1, Sensitivity.Balanced, RevisionCause.Created, null, null, 0));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SheetAssistMembers_RoundTrip()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var sheet = NewSheet();
        sheet.SetAssistSettings(Sensitivity.Loose, true, new RhythmTarget(92, 4, 2, 1.5));
        sheet.AddMark(IntentionalMark.Create(Guid.NewGuid(), IntentionalScope.Phrase, "regel een", "mine", Now));
        sheet.ConfirmOwnWork(OwnWorkOverride.Create("Robin", new CorpusTrack(Guid.NewGuid(), "Paper Lanterns"), Now));
        var id = Guid.NewGuid();
        sheet.ApplyAccepted(SuggestionApplier.Apply(sheet.Title, sheet.Text, [new ApplicableSuggestion(id, new Anchor(2, 6, 4, "twee"), SuggestionOperation.Replace, "drie")]), Now);
        await using (var write = db.NewContext())
        {
            await new LyricsSheetRepository(write).AddAsync(sheet, Ct);
        }

        await using var read = db.NewContext();
        var loaded = (await new LyricsSheetRepository(read).FindAsync(sheet.Id, Ct))!;

        (loaded.Revision, loaded.Sensitivity, loaded.RhythmTarget, loaded.LastCause, loaded.Text).ShouldBe((2, Sensitivity.Loose, new RhythmTarget(92, 4, 2, 1.5), RevisionCause.Accept, "regel een\nregel drie"));
        loaded.IntentionalMarks.Single().ShouldSatisfyAllConditions(m => m.Text.ShouldBe("regel een"), m => m.Note.ShouldBe("mine"));
        loaded.OwnWorkOverride!.ConfirmedBy.ShouldBe("Robin");
        loaded.AssistedLineNumbers().ShouldBe([(2, id)]);
    }

    [Fact]
    public async Task Revisions_KeepTheLast50()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var sheet = NewSheet();
        await using var context = db.NewContext();
        await new LyricsSheetRepository(context).AddAsync(sheet, Ct);
        var revisions = new LyricsRevisionRepository(context);
        for (var i = 0; i < 55; i++)
        {
            await revisions.AddAsync(LyricsSheetRevision.Of(sheet, RevisionCause.UserSave, null, Now), Ct);
            await context.SaveChangesAsync(Ct);
            sheet.Update(new LyricsContent("Song", "nl", $"tekst {i}"), Now);
        }

        await revisions.AddAsync(LyricsSheetRevision.Of(sheet, RevisionCause.UserSave, null, Now), Ct);
        await revisions.AddAsync(LyricsSheetRevision.Of(sheet, RevisionCause.UserSave, null, Now), Ct);
        await context.SaveChangesAsync(Ct);

        var list = await revisions.ListAsync(sheet.Id, Ct);
        (list.Count, list[0].Revision, list[^1].Revision).ShouldBe((50, 56, 7));
        (await revisions.FindAsync(sheet.Id, 56, Ct))!.Text.ShouldBe("tekst 54");
    }

    [Fact]
    public async Task DeletingTheSheet_CascadesToEverything()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var context = db.NewContext();
        var sheet = NewSheet();
        sheet.AddMark(IntentionalMark.Create(Guid.NewGuid(), IntentionalScope.Word, "regel", null, Now));
        var sheets = new LyricsSheetRepository(context);
        await sheets.AddAsync(sheet, Ct);
        var assist = await AddAssistWithSuggestionAsync(context, sheet);
        await new LyricsRevisionRepository(context).AddAsync(LyricsSheetRevision.Of(sheet, RevisionCause.Created, null, Now), Ct);
        await context.SaveChangesAsync(Ct);

        await sheets.RemoveAsync(sheet, Ct);

        foreach (var table in new[] { "LyricAssists", "LyricSuggestions", "LyricAssistEvents", "LyricsSheetRevisions", "LyricsIntentionalMarks", "LyricsAssistedLines" })
        {
            (await db.ScalarAsync($"SELECT COUNT(*) FROM {table}")).ShouldBe("0", table);
        }

        assist.ShouldNotBeNull();
    }

    [Fact]
    public async Task Assists_ListActivePruneAndEvents()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var context = db.NewContext();
        var sheet = NewSheet();
        await new LyricsSheetRepository(context).AddAsync(sheet, Ct);
        var repo = new LyricAssistRepository(context);
        var withOpen = await AddAssistWithSuggestionAsync(context, sheet);
        withOpen.Complete(Now);
        for (var i = 1; i <= 3; i++)
        {
            var assist = AssistRun.Create(Guid.NewGuid(), sheet, new AssistRequest { Kind = AssistKind.Check }, Now.AddMinutes(i));
            if (i < 3)
            {
                assist.Complete(Now);
            }

            await repo.AddAsync(assist, Ct);
        }

        (await repo.ActiveAsync(Ct)).Count.ShouldBe(1);
        (await repo.PruneAsync(sheet.Id, 1, Ct)).ShouldBe(2);
        (await repo.ListForSheetAsync(sheet.Id, 20, Ct)).Count.ShouldBe(2);
        (await repo.EventsAsync(withOpen.Id, 0, Ct)).Single().Seq.ShouldBe(1);
        (await repo.EventsAsync(withOpen.Id, 1, Ct)).ShouldBeEmpty();
        (await repo.LastSeqAsync(withOpen.Id, Ct), await repo.LastSeqAsync(Guid.NewGuid(), Ct)).ShouldBe((1, 0));
        (await repo.FindAsync(withOpen.Id, Ct)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Suggestions_FilterAndCount()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var context = db.NewContext();
        var sheet = NewSheet();
        await new LyricsSheetRepository(context).AddAsync(sheet, Ct);
        var assist = await AddAssistWithSuggestionAsync(context, sheet);
        var repo = new LyricSuggestionRepository(context);
        var all = await repo.ForAssistAsync(assist.Id, Ct);

        (await repo.ForSheetAsync(sheet.Id, SuggestionStatus.Open, SuggestionCategory.Spelling, Ct)).Count.ShouldBe(1);
        (await repo.ForSheetAsync(sheet.Id, SuggestionStatus.Rejected, null, Ct)).ShouldBeEmpty();
        (await repo.ForSheetAsync(sheet.Id, null, SuggestionCategory.Grammar, Ct)).ShouldBeEmpty();
        (await repo.FindManyAsync(sheet.Id, [all[0].Id, Guid.NewGuid()], Ct)).Count.ShouldBe(1);
        all[0].Decide(SuggestionStatus.Rejected, null, Now);
        await repo.SaveAsync(Ct);
        (await repo.CountOpenAsync(sheet.Id, Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task Words_AreUniquePerLanguage()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var context = db.NewContext();
        var words = new LyricWordRepository(context);
        await words.AddAsync(LyricWord.Create(Guid.NewGuid(), AssistLanguage.Dutch, "Zelmara", Now), Ct);
        await words.AddAsync(LyricWord.Create(Guid.NewGuid(), AssistLanguage.Dutch, "effe", Now), Ct);
        await words.AddAsync(LyricWord.Create(Guid.NewGuid(), AssistLanguage.English, "Zelmara", Now), Ct);

        await Should.ThrowAsync<DuplicateKeyException>(() => words.AddAsync(LyricWord.Create(Guid.NewGuid(), AssistLanguage.Dutch, "Zelmara", Now), Ct));
        (await words.ListAsync(AssistLanguage.Dutch, Ct)).Select(w => w.Word).ShouldBe(["Zelmara", "effe"]);
        (await words.CountAsync(AssistLanguage.English, Ct)).ShouldBe(1);
        var english = (await words.ListAsync(AssistLanguage.English, Ct)).Single();
        var tracked = (await words.FindAsync(english.Id, Ct))!;
        await words.RemoveAsync(tracked, Ct);
        (await words.CountAsync(AssistLanguage.English, Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task Settings_IsOneRow()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using (var write = db.NewContext())
        {
            var repo = new LyricWordRepository(write);
            (await repo.SettingsAsync(Ct)).SetClaudeNotice(true, "lyric-claude-notice.v1", Now);
            await repo.SaveAsync(Ct);
        }

        await using var read = db.NewContext();
        var settings = await new LyricWordRepository(read).SettingsAsync(Ct);
        settings.HasAcknowledged("lyric-claude-notice.v1").ShouldBeTrue();
        (await db.ScalarAsync("SELECT COUNT(*) FROM LyricAssistSettings")).ShouldBe("1");
    }

    private static async Task<AssistRun> AddAssistWithSuggestionAsync(StemStudioDbContext context, LyricsSheet sheet)
    {
        var assist = AssistRun.Create(Guid.NewGuid(), sheet, new AssistRequest { Kind = AssistKind.Check }, Now);
        assist.Start([new AssistLayer(SuggestionSource.Dictionary, "OpenTaal (nl)", true)], 1, Now);
        var repo = new LyricAssistRepository(context);
        await repo.AddAsync(assist, Ct);
        var suggestions = new LyricSuggestionRepository(context);
        suggestions.AddRange([LyricSuggestion.Create(Guid.NewGuid(), assist, new SuggestionDraft
        {
            Type = SuggestionType.SpellingTypo, Severity = SuggestionSeverity.Error, Anchor = new Anchor(1, 0, 5, "regel"), Operation = SuggestionOperation.Replace,
            Proposal = "regels", Explanation = "Typo", Source = SuggestionSource.Dictionary, RhymeWith = new RhymePartner(2, "twee"),
        }, Now)]);
        await suggestions.SaveAsync(Ct);
        await repo.AddEventAsync(AssistEvent.Create(assist.Id, 1, "assist.started", "{}", Now), Ct);
        return assist;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(Ct);
    }
}

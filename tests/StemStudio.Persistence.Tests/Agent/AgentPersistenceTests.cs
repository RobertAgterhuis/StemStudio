using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StemStudio.Persistence.Agent;

namespace StemStudio.Persistence.Tests.Agent;

/// <summary>011 T018: agent settings, conversations, messages, runs, steps, approvals, undo entries and run events (migration AddAgent).</summary>
public sealed class AgentPersistenceTests
{
    private const string LastMigrationOf010 = "20261115200748_AddLyricsAndVoices";
    private static readonly DateTime T0 = TestData.T0;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Settings_RoundTripTheSingleRow()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var settings = AgentRows.Settings();
        await using (var write = db.NewContext())
        {
            write.AgentSettings.Add(settings);
            await write.SaveChangesAsync(Ct);
        }

        await using var read = db.NewContext();
        var loaded = await read.AgentSettings.SingleAsync(Ct);
        loaded.ShouldBeEquivalentTo(settings);
        loaded.ClaudeNoticeAcknowledgedAt!.Value.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task ConversationMessagesAndRun_RoundTripInSeqOrder()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var conversation = AgentRows.Conversation(T0);
        var run = AgentRows.Run(conversation.Id, T0);
        conversation.ActiveRunId = run.Id;
        await using (var write = db.NewContext())
        {
            write.AgentConversations.Add(conversation);
            write.AgentMessages.Add(AgentRows.Message(conversation.Id, 2, "assistant", run.Id));
            write.AgentMessages.Add(AgentRows.Message(conversation.Id, 1, "user", null));
            write.AgentRuns.Add(run);
            await write.SaveChangesAsync(Ct);
        }

        await using var read = db.NewContext();
        (await read.AgentConversations.SingleAsync(Ct)).ShouldBeEquivalentTo(conversation);
        var messages = await read.AgentMessages.Where(m => m.ConversationId == conversation.Id).OrderBy(m => m.Seq).ToListAsync(Ct);
        messages.Select(m => (m.Seq, m.Role)).ShouldBe([(1, "user"), (2, "assistant")]);
        (messages[0].Page, messages[0].TrackId, messages[0].MixId).ShouldBe(("mixer", AgentRows.TrackId, AgentRows.MixId));
        (await read.AgentRuns.SingleAsync(Ct)).ShouldBeEquivalentTo(run);
    }

    [Fact]
    public async Task MessageSeq_IsUniquePerConversation()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var conversation = AgentRows.Conversation(T0);
        await using var write = db.NewContext();
        write.AgentConversations.Add(conversation);
        write.AgentMessages.Add(AgentRows.Message(conversation.Id, 1, "user", null));
        write.AgentMessages.Add(AgentRows.Message(conversation.Id, 1, "user", null));

        await Should.ThrowAsync<DbUpdateException>(() => write.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task StepsApprovalsAndUndoEntries_RoundTrip()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var conversation = AgentRows.Conversation(T0);
        var run = AgentRows.Run(conversation.Id, T0);
        var step = AgentRows.Step(run.Id, 1);
        var approval = AgentRows.Approval(step.Id, run.Id, T0);
        var undo = AgentRows.Undo(conversation.Id, run.Id, step.Id, 1);
        await using (var write = db.NewContext())
        {
            write.AgentConversations.Add(conversation);
            write.AgentRuns.Add(run);
            write.AgentRunSteps.Add(step);
            write.AgentApprovals.Add(approval);
            write.AgentUndoEntries.Add(undo);
            await write.SaveChangesAsync(Ct);
        }

        await using var read = db.NewContext();
        var loadedStep = await read.AgentRunSteps.SingleAsync(Ct);
        loadedStep.ShouldBeEquivalentTo(step);
        loadedStep.ArgumentsHash.Length.ShouldBe(64);
        (await read.AgentApprovals.SingleAsync(Ct)).ShouldBeEquivalentTo(approval);
        (await read.AgentUndoEntries.SingleAsync(Ct)).ShouldBeEquivalentTo(undo);
    }

    [Fact]
    public async Task RunEvents_AppendWithTheNextSeqAndReplayAfter()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var conversation = AgentRows.Conversation(T0);
        var run = AgentRows.Run(conversation.Id, T0);
        var other = AgentRows.Run(conversation.Id, T0);
        await using (var write = db.NewContext())
        {
            write.AgentConversations.Add(conversation);
            write.AgentRuns.AddRange(run, other);
            await write.SaveChangesAsync(Ct);
        }

        await using var context = db.NewContext();
        var log = new RunEventLog(context);
        (await log.AppendAsync(Event(run.Id, "run.started", "{}", T0), Ct)).Seq.ShouldBe(1);
        (await log.AppendAsync(Event(other.Id, "run.started", "{}", T0), Ct)).Seq.ShouldBe(1);
        (await log.AppendAsync(Event(run.Id, "text.delta", """{"text":"Hi"}""", T0.AddSeconds(1)), Ct)).Seq.ShouldBe(2);
        (await log.AppendAsync(Event(run.Id, "run.completed", "{}", T0.AddSeconds(2)), Ct)).Seq.ShouldBe(3);

        (await log.ListAfterAsync(run.Id, 0, Ct)).Select(e => e.Type).ShouldBe(["run.started", "text.delta", "run.completed"]);
        var replay = await log.ListAfterAsync(run.Id, 1, Ct);
        replay.Select(e => e.Seq).ShouldBe([2, 3]);
        replay[0].Payload.ShouldBe("""{"text":"Hi"}""");
        replay[0].CreatedAt.ShouldBe(T0.AddSeconds(1));
        (await log.ListAfterAsync(run.Id, 3, Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task DeletingAConversation_CascadesToEverythingOfIt()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var conversation = AgentRows.Conversation(T0);
        var run = AgentRows.Run(conversation.Id, T0);
        var step = AgentRows.Step(run.Id, 1);
        await using (var write = db.NewContext())
        {
            write.AgentConversations.Add(conversation);
            write.AgentMessages.Add(AgentRows.Message(conversation.Id, 1, "user", null));
            write.AgentRuns.Add(run);
            write.AgentRunSteps.Add(step);
            write.AgentApprovals.Add(AgentRows.Approval(step.Id, run.Id, T0));
            write.AgentUndoEntries.Add(AgentRows.Undo(conversation.Id, run.Id, step.Id, 1));
            await write.SaveChangesAsync(Ct);
            await new RunEventLog(write).AppendAsync(Event(run.Id, "run.started", "{}", T0), Ct);
        }

        await using (var delete = db.NewContext())
        {
            await delete.AgentConversations.Where(c => c.Id == conversation.Id).ExecuteDeleteAsync(Ct);
        }

        foreach (var table in new[] { "Conversations", "ConversationMessages", "AgentRuns", "RunSteps", "Approvals", "UndoEntries", "RunEvents" })
        {
            (await db.ScalarAsync($"SELECT COUNT(*) FROM {table}")).ShouldBe("0", table);
        }
    }

    [Fact]
    public async Task DatabaseFrom010_MigratesToTheAgentTables()
    {
        var path = Path.Combine(Path.GetTempPath(), $"stemstudio-010-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Pooling=False";
        try
        {
            await SqliteTestDatabase.MigrateLegacyAsync(connectionString, LastMigrationOf010);

            await using var db = await SqliteTestDatabase.CreateAsync(connectionString);
            await using var read = db.NewContext();
            read.Database.GetPendingMigrations().ShouldBeEmpty();
            (await read.AgentSettings.AnyAsync(Ct)).ShouldBeFalse();
            (await read.AgentConversations.AnyAsync(Ct)).ShouldBeFalse();
            (await db.ScalarAsync("SELECT COUNT(*) FROM Tracks")).ShouldBe("1");
            read.AgentSettings.Add(AgentRows.Settings());
            await read.SaveChangesAsync(Ct);
            (await db.ScalarAsync("SELECT COUNT(*) FROM AgentSettings")).ShouldBe("1");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static RunEventEntity Event(Guid runId, string type, string payload, DateTime createdAt) =>
        new() { RunId = runId, Type = type, Payload = payload, CreatedAt = createdAt };
}

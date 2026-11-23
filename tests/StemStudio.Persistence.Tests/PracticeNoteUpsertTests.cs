using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StemStudio.Domain.Handouts;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence.Tests;

/// <summary>
/// Security review 020, finding 3: <see cref="PracticeNoteRepository.SetAsync"/> is an upsert that survives a concurrent first
/// write or delete of the same note, and <see cref="PracticeNoteRepository.DeleteAsync"/> is idempotent.
/// </summary>
public sealed class PracticeNoteUpsertTests
{
    private static readonly DateTime T0 = TestData.T0;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SetAsync_InsertsThenUpdates_AndDeleteAsyncIsIdempotent()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.CompletedTrack();
        await db.SeedAsync(track);

        await using (var write = db.NewContext())
        {
            (await new PracticeNoteRepository(write).SetAsync(PracticeNote.Create(track.Id, HandoutFamily.Bass, "First.", T0), Ct)).Text.ShouldBe("First.");
        }

        await using (var update = db.NewContext())
        {
            var stored = await new PracticeNoteRepository(update).SetAsync(PracticeNote.Create(track.Id, HandoutFamily.Bass, "Second.", T0.AddMinutes(1)), Ct);
            (stored.Text, stored.UpdatedAt).ShouldBe(("Second.", T0.AddMinutes(1)));
        }

        (await db.ScalarAsync("SELECT COUNT(*) || ':' || Text FROM PracticeNotes")).ShouldBe("1:Second.");
        await using (var delete = db.NewContext())
        {
            var notes = new PracticeNoteRepository(delete);
            await notes.DeleteAsync(track.Id, HandoutFamily.Bass, Ct);
            await notes.DeleteAsync(track.Id, HandoutFamily.Bass, Ct);
        }

        (await db.ScalarAsync("SELECT COUNT(*) FROM PracticeNotes")).ShouldBe("0");
    }

    [Fact]
    public async Task SetAsync_AfterAConcurrentFirstWrite_UpdatesTheStoredNote()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.CompletedTrack();
        await db.SeedAsync(track);
        var race = new RacingWrite(async () =>
        {
            await using var other = db.NewContext();
            other.PracticeNotes.Add(PracticeNote.Create(track.Id, HandoutFamily.Drums, "Other.", T0));
            await other.SaveChangesAsync(Ct);
        });

        await using (var write = db.NewContext(race))
        {
            (await new PracticeNoteRepository(write).SetAsync(PracticeNote.Create(track.Id, HandoutFamily.Drums, "Mine.", T0.AddMinutes(1)), Ct)).Text.ShouldBe("Mine.");
        }

        race.Saves.ShouldBe(2);
        (await db.ScalarAsync("SELECT COUNT(*) || ':' || Text FROM PracticeNotes")).ShouldBe("1:Mine.");
    }

    [Fact]
    public async Task SetAsync_AfterAConcurrentDelete_InsertsTheNoteAgain()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.CompletedTrack();
        await db.SeedAsync(track);
        await using (var seed = db.NewContext())
        {
            await new PracticeNoteRepository(seed).SetAsync(PracticeNote.Create(track.Id, HandoutFamily.Keys, "Old.", T0), Ct);
        }

        var race = new RacingWrite(async () =>
        {
            await using var other = db.NewContext();
            await new PracticeNoteRepository(other).DeleteAsync(track.Id, HandoutFamily.Keys, Ct);
        });
        await using (var write = db.NewContext(race))
        {
            await new PracticeNoteRepository(write).SetAsync(PracticeNote.Create(track.Id, HandoutFamily.Keys, "New.", T0.AddMinutes(1)), Ct);
        }

        race.Saves.ShouldBe(2);
        (await db.ScalarAsync("SELECT COUNT(*) || ':' || Text FROM PracticeNotes")).ShouldBe("1:New.");
    }

    [Fact]
    public async Task SetAsync_PassesTheConflictOn_AfterTheLastAttempt()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var track = TestData.CompletedTrack();
        await db.SeedAsync(track);
        var failing = new RacingWrite(() => throw new DbUpdateException("The database is busy."), always: true);

        await using var write = db.NewContext(failing);
        await Should.ThrowAsync<DbUpdateException>(() => new PracticeNoteRepository(write).SetAsync(PracticeNote.Create(track.Id, HandoutFamily.Vocals, "x", T0), Ct));

        failing.Saves.ShouldBe(PracticeNoteRepository.MaxSetAttempts);
    }

    /// <summary>Runs a competing write right before the context's own save (once, or before every save).</summary>
    private sealed class RacingWrite(Func<Task> competing, bool always = false) : SaveChangesInterceptor
    {
        public int Saves { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Saves++;
            if (always || Saves == 1)
            {
                await competing();
            }

            return result;
        }
    }
}

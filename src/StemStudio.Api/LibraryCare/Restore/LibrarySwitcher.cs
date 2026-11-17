using Microsoft.Data.Sqlite;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.LibraryCare.Restore;

/// <summary>Test seam called after every journal step (crash injection, SC-005).</summary>
public interface IJournalStepHook
{
    /// <summary>Called after a step was moved and recorded.</summary>
    /// <param name="index">Step index.</param>
    /// <param name="gate">The gate (in maintenance).</param>
    void AfterStep(int index, LibraryGate gate);
}

/// <summary>The production hook: does nothing.</summary>
public sealed class NoJournalStepHook : IJournalStepHook
{
    /// <inheritdoc />
    public void AfterStep(int index, LibraryGate gate)
    {
    }
}

/// <summary>
/// Switches libraries in one journalled step (research R-04 P3/P4, R-07): maintenance gate, wait for in-flight requests,
/// <c>ClearAllPools()</c>, each move retried 10 x 200 ms; on failure every done move is undone and the job fails with
/// <c>io-error</c>. Requests still in flight after 10 s fail the switch with <c>io-error</c> before anything moves. The gate
/// is opened in every outcome. An item created at a live path during the switch is replaced only when it is fresh and empty
/// (<see cref="FreshItems"/>); otherwise the switch fails and is rolled back, never deleting data.
/// </summary>
/// <param name="gate">The gate.</param>
/// <param name="journal">Journal files.</param>
/// <param name="files">File seam.</param>
/// <param name="hook">Step hook.</param>
/// <param name="retryDelay">Delay between move attempts.</param>
public sealed class LibrarySwitcher(LibraryGate gate, RestoreJournal journal, ILibraryFiles files, IJournalStepHook hook, TimeSpan retryDelay)
{
    /// <summary>Attempts per move.</summary>
    public const int MoveAttempts = 10;

    /// <summary>Error text of a failed switch whose rollback put everything back.</summary>
    public const string NotChangedText = "A library file is in use and could not be moved; the library was not changed. Try again.";

    /// <summary>Error text of a failed switch whose rollback could not put everything back (start-up recovery finishes it).</summary>
    public const string NotRestoredText =
        "A library file is in use and could not be moved back; nothing was deleted. Restart StemStudio to finish putting your library back.";

    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Runs after a committed switch while the gate is still closed (the new live database is put in WAL mode).</summary>
    public Action? AfterSwitch { get; init; }

    /// <summary>Runs the plan.</summary>
    /// <param name="plan">The journal.</param>
    /// <param name="cancellationToken">Cancellation (not observed once moves started).</param>
    /// <returns>A task.</returns>
    public async Task SwitchAsync(RestoreJournalRecord plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var record = JournalPlans.Validate(plan) with { State = JournalState.Switching };
        await journal.SaveAsync(record, cancellationToken);
        gate.EnterMaintenance();
        try
        {
            if (!await gate.WaitForIdleAsync(IdleTimeout, cancellationToken))
            {
                await journal.SaveAsync(record with { State = JournalState.RolledBack }, CancellationToken.None);
                throw new LibraryJobException(LibraryJobErrorCodeDto.IoError, "Requests to the library did not finish in time; the library was not changed. Try again.");
            }

            SqliteConnection.ClearAllPools();
            await MoveAllAsync(record);
            AfterSwitch?.Invoke();
        }
        finally
        {
            gate.ExitMaintenance();
        }
    }

    private async Task MoveAllAsync(RestoreJournalRecord record)
    {
        var steps = record.Steps.ToList();
        var mover = new JournalMover(files, retryDelay);
        for (var index = 0; index < steps.Count; index++)
        {
            if (!await TryMoveAsync(steps[index], record, mover))
            {
                throw await RollBackAsync(record with { Steps = steps });
            }

            steps[index] = steps[index] with { Done = true };
            await journal.SaveAsync(record with { Steps = steps }, CancellationToken.None);
            hook.AfterStep(index, gate);
        }

        await journal.SaveAsync(record with { Steps = steps, State = JournalState.Committed }, CancellationToken.None);
        journal.FinishCommitted(record);
    }

    private async Task<LibraryJobException> RollBackAsync(RestoreJournalRecord record)
    {
        var restored = await journal.RollBackAsync(record, files, CancellationToken.None);
        return new LibraryJobException(LibraryJobErrorCodeDto.IoError, restored ? NotChangedText : NotRestoredText);
    }

    private Task<bool> TryMoveAsync(JournalStep step, RestoreJournalRecord record, JournalMover mover) =>
        mover.MoveAsync(journal.Full(step.From), journal.Full(step.To), Interferers(step, record));

    /// <summary>
    /// What may be in the way of a move in: every live original was moved aside before, so an item at the destination (and,
    /// for the database, a companion no step moves in) was created during the switch; it is removed when fresh and empty.
    /// </summary>
    private List<string> Interferers(JournalStep step, RestoreJournalRecord record)
    {
        if (step.Op != JournalOp.MoveIn)
        {
            return [];
        }

        var movedIn = record.Steps.Where(other => other.Op == JournalOp.MoveIn).Select(other => other.To).ToHashSet(StringComparer.Ordinal);
        var companions = step.To == JournalPlans.Items[0] ? FreshItems.DatabaseCompanions.Where(companion => !movedIn.Contains(companion)) : [];
        return [.. companions.Prepend(step.To).Select(journal.Full)];
    }
}

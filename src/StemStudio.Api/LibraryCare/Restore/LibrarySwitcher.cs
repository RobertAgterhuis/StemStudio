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
/// <c>io-error</c>. The gate is opened in every outcome.
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

    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(10);

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
            await gate.WaitForIdleAsync(IdleTimeout, cancellationToken);
            SqliteConnection.ClearAllPools();
            await MoveAllAsync(record);
        }
        finally
        {
            gate.ExitMaintenance();
        }
    }

    private async Task MoveAllAsync(RestoreJournalRecord record)
    {
        var steps = record.Steps.ToList();
        for (var index = 0; index < steps.Count; index++)
        {
            if (!await TryMoveAsync(steps[index]))
            {
                await journal.RollBackAsync(record with { Steps = steps }, files, CancellationToken.None);
                throw new LibraryJobException(LibraryJobErrorCodeDto.IoError, "A library file is in use and could not be moved; the library was not changed. Try again.");
            }

            steps[index] = steps[index] with { Done = true };
            await journal.SaveAsync(record with { Steps = steps }, CancellationToken.None);
            hook.AfterStep(index, gate);
        }

        await journal.SaveAsync(record with { Steps = steps, State = JournalState.Committed }, CancellationToken.None);
        Finish(record);
    }

    private async Task<bool> TryMoveAsync(JournalStep step)
    {
        var paths = journal;
        if (!Path.Exists(paths.Full(step.From)))
        {
            // A -wal or -shm file disappears when the last connection closes; nothing to move.
            return true;
        }

        for (var attempt = 0; attempt < MoveAttempts; attempt++)
        {
            try
            {
                files.Move(paths.Full(step.From), paths.Full(step.To));
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(retryDelay);
            }
        }

        return false;
    }

    private void Finish(RestoreJournalRecord record)
    {
        var remove = record.KeepPrevious ? record.RemoveAfterCommit : $"library-care/previous/{LibraryCarePaths.Id(record.RestoreId)}";
        if (remove is not null && Directory.Exists(journal.Full(remove)))
        {
            Directory.Delete(journal.Full(remove), recursive: true);
        }
    }
}

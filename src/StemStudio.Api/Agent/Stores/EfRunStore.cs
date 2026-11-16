using Microsoft.EntityFrameworkCore;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Persistence;
using StemStudio.Persistence.Agent;

namespace StemStudio.Api.Agent.Stores;

/// <summary>EF Core implementation of <see cref="IRunStore"/> (feature 011, T021): at most one non-terminal run globally.</summary>
/// <param name="db">Context per operation.</param>
public sealed class EfRunStore(AgentDb db) : IRunStore, IDisposable
{
    /// <summary>Stored names of the terminal run states.</summary>
    internal static readonly string[] TerminalStatuses = ["Completed", "Failed", "Cancelled", "Interrupted"];

    private readonly SemaphoreSlim _createLock = new(1, 1);

    /// <inheritdoc />
    public async Task CreateRunAsync(AgentRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        await _createLock.WaitAsync(cancellationToken);
        try
        {
            if (!run.IsTerminal && await GetActiveRunAsync(cancellationToken) is { } active)
            {
                throw new InvalidOperationException($"Run {active.Id} is still active.");
            }

            await db.SaveAsync(context => Task.FromResult(context.AgentRuns.Add(RunMapping.Apply(new(), run))), cancellationToken);
        }
        finally
        {
            _createLock.Release();
        }
    }

    /// <inheritdoc />
    public Task<AgentRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken) => db.WithAsync(async context =>
    {
        var entity = await context.AgentRuns.AsNoTracking().SingleOrDefaultAsync(r => r.Id == runId, cancellationToken);
        return entity is null ? null : RunMapping.ToRecord(entity);
    });

    /// <inheritdoc />
    public Task UpdateRunAsync(AgentRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        return db.SaveAsync(async context =>
        {
            var entity = await context.AgentRuns.SingleOrDefaultAsync(r => r.Id == run.Id, cancellationToken)
                ?? throw new KeyNotFoundException($"Run {run.Id} does not exist.");
            RunMapping.Apply(entity, run);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<AgentRun?> GetActiveRunAsync(CancellationToken cancellationToken) => db.WithAsync(async context =>
    {
        var entity = await ActiveRuns(context).OrderBy(r => r.StartedAt).FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : RunMapping.ToRecord(entity);
    });

    /// <inheritdoc />
    public Task SaveStepAsync(RunStep runStep, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runStep);
        return db.SaveAsync(async context =>
        {
            var entity = await context.AgentRunSteps.SingleOrDefaultAsync(s => s.Id == runStep.Id, cancellationToken);
            StepMapping.Apply(entity ?? context.AgentRunSteps.Add(new RunStepEntity()).Entity, runStep);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<RunStep>> GetStepsAsync(Guid runId, CancellationToken cancellationToken) =>
        db.WithAsync<IReadOnlyList<RunStep>>(async context =>
        {
            var rows = await context.AgentRunSteps.AsNoTracking().Where(s => s.RunId == runId).OrderBy(s => s.Seq).ToListAsync(cancellationToken);
            return [.. rows.Select(StepMapping.ToRecord)];
        });

    /// <inheritdoc />
    public Task SaveApprovalAsync(Approval approval, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approval);
        return db.SaveAsync(async context =>
        {
            var entity = await context.AgentApprovals.SingleOrDefaultAsync(a => a.Id == approval.Id, cancellationToken);
            RunMapping.Apply(entity ?? context.AgentApprovals.Add(new ApprovalEntity()).Entity, approval);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Approval?> GetApprovalAsync(Guid stepId, CancellationToken cancellationToken) => db.WithAsync(async context =>
    {
        var entity = await context.AgentApprovals.AsNoTracking().SingleOrDefaultAsync(a => a.Id == stepId, cancellationToken);
        return entity is null ? null : RunMapping.ToRecord(entity);
    });

    /// <inheritdoc />
    public void Dispose() => _createLock.Dispose();

    /// <summary>Runs that are not terminal.</summary>
    /// <param name="context">The context.</param>
    /// <returns>The query.</returns>
    internal static IQueryable<AgentRunEntity> ActiveRuns(StemStudioDbContext context) =>
        context.AgentRuns.Where(r => !TerminalStatuses.Contains(r.Status));
}

using Microsoft.EntityFrameworkCore;
using StemStudio.Agent.Ports;
using StemStudio.Api.Agent.Stores;
using StemStudio.Contracts.Agent;
using StemStudio.Persistence;
using StemStudio.Persistence.Agent;

namespace StemStudio.Api.Agent;

/// <summary>
/// API start-up step of feature 011 (data-model 4.1, plan.md "Restart"): every non-terminal run becomes <c>interrupted</c>, its
/// pending approvals and suspended steps <c>expired</c> (running steps fail), the conversation loses its active run, and a
/// <c>run.interrupted</c> event closes open streams. Jobs the run started keep running.
/// </summary>
/// <param name="db">Context per operation.</param>
/// <param name="events">Event sink.</param>
/// <param name="clock">Clock.</param>
public sealed class AgentStartupRecovery(AgentDb db, IRunEventSink events, TimeProvider clock)
{
    /// <summary>Readable error of a step that was running when the API stopped.</summary>
    public const string InterruptedStepError = "StemStudio restarted while this step was running.";

    private static readonly string[] SuspendedStepStatuses = [nameof(AgentStepStatusDto.AwaitingApproval)];

    private static readonly string[] TerminalStepStatuses =
    [
        nameof(AgentStepStatusDto.Succeeded), nameof(AgentStepStatusDto.Failed), nameof(AgentStepStatusDto.Rejected),
        nameof(AgentStepStatusDto.RejectedInvalid), nameof(AgentStepStatusDto.Skipped), nameof(AgentStepStatusDto.Expired),
        nameof(AgentStepStatusDto.Undone),
    ];

    /// <summary>Interrupts every non-terminal run.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The ids of the interrupted runs.</returns>
    public async Task<IReadOnlyList<Guid>> RecoverAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var runIds = await db.WithAsync(async context =>
        {
            var runs = await EfRunStore.ActiveRuns(context).ToListAsync(cancellationToken);
            foreach (var run in runs)
            {
                await InterruptAsync(context, run, now, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);
            return runs.Select(run => run.Id).ToList();
        });
        foreach (var runId in runIds)
        {
            await events.PublishAsync(runId, AgentEventTypes.RunInterrupted, AgentStoreValues.ToJson(new RunInterruptedEventDto { RunId = runId }), cancellationToken);
        }

        return runIds;
    }

    private static async Task InterruptAsync(StemStudioDbContext context, AgentRunEntity run, DateTime now, CancellationToken cancellationToken)
    {
        run.Status = nameof(AgentRunStatusDto.Interrupted);
        run.EndedAt = now;
        await context.AgentApprovals.Where(a => a.RunId == run.Id && a.Status == nameof(ApprovalStatusDto.Pending))
            .ForEachAsync(a => a.Status = nameof(ApprovalStatusDto.Expired), cancellationToken);
        await context.AgentRunSteps.Where(s => s.RunId == run.Id && !TerminalStepStatuses.Contains(s.Status))
            .ForEachAsync(step => EndStep(step, now), cancellationToken);
        await context.AgentConversations.Where(c => c.ActiveRunId == run.Id)
            .ForEachAsync(c => c.ActiveRunId = null, cancellationToken);
    }

    private static void EndStep(RunStepEntity step, DateTime now)
    {
        var suspended = SuspendedStepStatuses.Contains(step.Status);
        step.Status = suspended ? nameof(AgentStepStatusDto.Expired) : nameof(AgentStepStatusDto.Failed);
        step.Error = suspended ? step.Error : InterruptedStepError;
        step.EndedAt = now;
    }
}

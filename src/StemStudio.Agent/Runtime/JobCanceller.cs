using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Tools.Providers;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Runtime;

/// <summary>Cancels the jobs a run started (cancel with <c>cancelJobs: true</c>, FR-010); best effort.</summary>
internal static class JobCanceller
{
    /// <summary>Cancels the cancellable jobs (detail separation, generations) and returns them as "phase:id".</summary>
    public static async Task<IReadOnlyList<string>> CancelAsync(AgentServices services, Guid runId)
    {
        var steps = await services.Runs.GetStepsAsync(runId, CancellationToken.None);
        var cancelled = new List<string>();
        foreach (var job in steps.Where(step => step.Tool != MetaToolProvider.WaitForJob).Select(step => step.Job).OfType<JobRef>())
        {
            if (await TryCancelAsync(services.Actions, job))
            {
                cancelled.Add($"{ToolDefinitions.Name(job.Phase)}:{job.JobId ?? job.TrackId?.ToString()}");
            }
        }

        return cancelled;
    }

    private static async Task<bool> TryCancelAsync(IStemStudioActions actions, JobRef job)
    {
        try
        {
            switch (job.Phase)
            {
                case JobPhaseDto.Detail when job.TrackId is { } trackId:
                    await actions.Tracks.CancelDetailAsync(trackId, CancellationToken.None);
                    return true;
                case JobPhaseDto.Restyle or JobPhaseDto.Generation when Guid.TryParse(job.JobId, out var generationId):
                    await actions.Generations.CancelGenerationAsync(generationId, CancellationToken.None);
                    return true;
                default:
                    return false;
            }
        }
        catch (AgentActionException)
        {
            return false;
        }
    }
}

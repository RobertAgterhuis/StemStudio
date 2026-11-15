namespace StemStudio.Contracts.Agent;

/// <summary>Body of POST /api/agent/runs/{runId}/cancel.</summary>
public sealed record CancelAgentRunRequest
{
    /// <summary>True to also cancel jobs the run started (where the feature can cancel).</summary>
    public bool CancelJobs { get; init; }
}

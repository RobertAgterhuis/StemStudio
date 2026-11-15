using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Model;

/// <summary>Background job a step started or waits for.</summary>
public sealed record JobRef
{
    /// <summary>Track of the job.</summary>
    public Guid? TrackId { get; init; }

    /// <summary>Job or library item identifier, when the feature has one.</summary>
    public string? JobId { get; init; }

    /// <summary>Kind of job.</summary>
    public required JobPhaseDto Phase { get; init; }
}

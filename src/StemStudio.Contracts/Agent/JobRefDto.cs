namespace StemStudio.Contracts.Agent;

/// <summary>Background job a step started or waits for.</summary>
public sealed record JobRefDto
{
    /// <summary>Track of the job.</summary>
    public Guid? TrackId { get; init; }

    /// <summary>Job or library item identifier, when the feature has one (007/008: asset id).</summary>
    public string? JobId { get; init; }

    /// <summary>Kind of job.</summary>
    public required JobPhaseDto Phase { get; init; }
}

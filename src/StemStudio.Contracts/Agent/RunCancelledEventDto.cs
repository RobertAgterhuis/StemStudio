namespace StemStudio.Contracts.Agent;

/// <summary>Data of the run.cancelled event.</summary>
public sealed record RunCancelledEventDto
{
    /// <summary>Run identifier.</summary>
    public required Guid RunId { get; init; }

    /// <summary>Jobs that were cancelled with the run (track or asset ids).</summary>
    public IReadOnlyList<string> CancelledJobs { get; init; } = [];
}

namespace StemStudio.Contracts.Agent;

/// <summary>Why a run failed (also the run.failed event).</summary>
public sealed record RunFailureDto
{
    /// <summary>Failure code, see <see cref="AgentFailureCodes"/>.</summary>
    public required string Code { get; init; }

    /// <summary>Readable message, at most 500 characters.</summary>
    public required string Message { get; init; }

    /// <summary>Readable fix, e.g. "Switch to local" after a Claude failure.</summary>
    public string? Fix { get; init; }
}

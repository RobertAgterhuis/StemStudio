namespace StemStudio.Contracts.LyricAssist;

/// <summary>Why an assist failed (<c>LyricAssistSummary.failure</c>).</summary>
public sealed record AssistFailureDto
{
    /// <summary>Failure code.</summary>
    public required AssistFailureCodeDto Code { get; init; }

    /// <summary>Readable message (at most 500 characters).</summary>
    public required string Message { get; init; }

    /// <summary>Readable fix.</summary>
    public string? Fix { get; init; }
}

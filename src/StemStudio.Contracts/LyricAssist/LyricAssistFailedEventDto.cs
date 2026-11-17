namespace StemStudio.Contracts.LyricAssist;

/// <summary>Data of the <c>assist.failed</c> event.</summary>
public sealed record LyricAssistFailedEventDto
{
    /// <summary>Failure code (<see cref="AssistFailureCodeDto"/> wire name).</summary>
    public required string Code { get; init; }

    /// <summary>Readable message (at most 500 characters).</summary>
    public required string Message { get; init; }

    /// <summary>Readable fix.</summary>
    public string? Fix { get; init; }
}

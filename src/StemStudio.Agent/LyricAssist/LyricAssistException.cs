using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.LyricAssist;

/// <summary>The assist failed; the backend records <see cref="Code"/> and the message as the assist's failure.</summary>
/// <param name="code">Failure code.</param>
/// <param name="message">Readable message (at most 500 characters).</param>
/// <param name="fix">Readable fix, if any.</param>
public sealed class LyricAssistException(AssistFailureCodeDto code, string message, string? fix = null) : Exception(message)
{
    /// <summary>Failure code.</summary>
    public AssistFailureCodeDto Code { get; } = code;

    /// <summary>Readable fix, if any.</summary>
    public string? Fix { get; } = fix;
}

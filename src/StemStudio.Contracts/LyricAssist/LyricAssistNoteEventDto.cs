namespace StemStudio.Contracts.LyricAssist;

/// <summary>Data of the <c>assist.note</c> event.</summary>
public sealed record LyricAssistNoteEventDto
{
    /// <summary>Note code.</summary>
    public required AssistNoteCodeDto Code { get; init; }

    /// <summary>Readable message (at most 300 characters).</summary>
    public required string Message { get; init; }
}

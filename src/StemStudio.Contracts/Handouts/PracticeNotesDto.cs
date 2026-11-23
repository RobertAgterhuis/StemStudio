namespace StemStudio.Contracts.Handouts;

/// <summary>The practice notes of a track (<c>getPracticeNotes</c>) (backend-api 1.19.0, feature 020).</summary>
public sealed record PracticeNotesDto
{
    /// <summary>One note per family that has one (at most 7).</summary>
    public required IReadOnlyList<PracticeNoteDto> Notes { get; init; }
}

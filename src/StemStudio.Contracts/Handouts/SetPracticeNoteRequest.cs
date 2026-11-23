namespace StemStudio.Contracts.Handouts;

/// <summary>Body of <c>setPracticeNote</c>; an empty or whitespace text deletes the note (backend-api 1.19.0, feature 020).</summary>
public sealed record SetPracticeNoteRequest
{
    /// <summary>Text (at most 4,000 characters).</summary>
    public required string Text { get; init; }
}

namespace StemStudio.Contracts.Handouts;

/// <summary>A practice note of one family (backend-api 1.19.0, feature 020).</summary>
public sealed record PracticeNoteDto
{
    /// <summary>Family.</summary>
    public required HandoutFamilyDto Family { get; init; }

    /// <summary>Text (1..4,000 characters).</summary>
    public required string Text { get; init; }

    /// <summary>Last change (UTC).</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}

namespace StemStudio.Contracts.LibraryCare;

/// <summary>A clean-up preview request (<c>CleanupRequest</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record CleanupRequestDto
{
    /// <summary>One to three actions.</summary>
    public required IReadOnlyList<CleanupActionDto> Actions { get; init; }
}

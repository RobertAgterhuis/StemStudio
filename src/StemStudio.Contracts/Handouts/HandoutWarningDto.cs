namespace StemStudio.Contracts.Handouts;

/// <summary>A renderer warning on a handout item (backend-api 1.19.0, feature 020).</summary>
public sealed record HandoutWarningDto
{
    /// <summary>Code.</summary>
    public required HandoutWarningCodeDto Code { get; init; }

    /// <summary>Text (at most 300 characters).</summary>
    public required string Text { get; init; }
}

namespace StemStudio.Contracts.Sections;

/// <summary>A warning of the last detection (backend-api 1.13.0, feature 014).</summary>
public sealed record SectionWarningDto
{
    /// <summary>Warning code.</summary>
    public required SectionWarningCodeDto Code { get; init; }

    /// <summary>Text shown in the section status (at most 300 characters).</summary>
    public required string Message { get; init; }
}

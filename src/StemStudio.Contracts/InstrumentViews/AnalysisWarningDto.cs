namespace StemStudio.Contracts.InstrumentViews;

/// <summary>A warning of an analysis (backend-api 1.15.0, feature 016).</summary>
public sealed record AnalysisWarningDto
{
    /// <summary>Code.</summary>
    public required AnalysisWarningCodeDto Code { get; init; }

    /// <summary>English text (at most 300 characters).</summary>
    public required string Text { get; init; }
}

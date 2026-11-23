namespace StemStudio.Contracts.Insights;

/// <summary>Count of stems per rating (backend-api 1.19.0, feature 020).</summary>
public sealed record QualitySummaryDto
{
    /// <summary>Stems rated good.</summary>
    public required int Good { get; init; }

    /// <summary>Stems rated fair.</summary>
    public required int Fair { get; init; }

    /// <summary>Stems rated poor.</summary>
    public required int Poor { get; init; }

    /// <summary>Stems probably not in the song.</summary>
    public required int NotPresent { get; init; }

    /// <summary>Stems not rated.</summary>
    public required int Unknown { get; init; }
}

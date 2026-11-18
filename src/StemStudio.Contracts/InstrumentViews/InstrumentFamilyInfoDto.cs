namespace StemStudio.Contracts.InstrumentViews;

/// <summary>One family of the instrument catalogue (backend-api 1.15.0, feature 016, data-model 5.3).</summary>
public sealed record InstrumentFamilyInfoDto
{
    /// <summary>Family id.</summary>
    public required InstrumentFamilyDto Id { get; init; }

    /// <summary>Display label.</summary>
    public required string Label { get; init; }

    /// <summary>Standard stems of the family (<c>other</c> only while it is not split).</summary>
    public required IReadOnlyList<string> Stems { get; init; }

    /// <summary>Detail names of the family (any parent).</summary>
    public required IReadOnlyList<string> DetailNames { get; init; }

    /// <summary>The view "Practise as" opens.</summary>
    public required InstrumentViewDto View { get; init; }
}

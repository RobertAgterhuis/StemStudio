namespace StemStudio.Contracts.InstrumentViews;

/// <summary><c>GET /api/instruments/catalogue</c> (<c>InstrumentCatalogue</c>, static) (backend-api 1.15.0, feature 016).</summary>
public sealed record InstrumentCatalogueDto
{
    /// <summary>Families in "Practise as" order.</summary>
    public required IReadOnlyList<InstrumentFamilyInfoDto> Families { get; init; }

    /// <summary>Tunings (data-model 4.1).</summary>
    public required IReadOnlyList<TuningDto> Tunings { get; init; }

    /// <summary>Written instruments, <c>concert</c> first (data-model 4.2).</summary>
    public required IReadOnlyList<WrittenInstrumentInfoDto> Instruments { get; init; }

    /// <summary>Clefs (data-model 4.3).</summary>
    public required IReadOnlyList<ClefChoiceDto> Clefs { get; init; }

    /// <summary>Singing tolerances.</summary>
    public SingingTolerancesDto Tolerances { get; init; } = new();
}

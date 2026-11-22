using StemStudio.Contracts.InstrumentViews;
using StemStudio.Domain.InstrumentViews;

namespace StemStudio.Api.InstrumentViews;

/// <summary>The static instrument catalogue of <c>getInstrumentCatalogue</c> (data-model 7.5), built once from the domain catalogue.</summary>
public static class CatalogueMapper
{
    private static readonly string[] ClefNames = ["auto", "treble", "bass", "alto", "tenor", "treble-8vb", "bass-8vb"];

    /// <summary>The catalogue.</summary>
    public static InstrumentCatalogueDto Catalogue { get; } = new()
    {
        Families = FamilyRule.Families.Select(Family).ToList(),
        Tunings = InstrumentCatalogue.Tunings.Select(Tuning).ToList(),
        Instruments = InstrumentCatalogue.Instruments.Select(Instrument).ToList(),
        Clefs = InstrumentCatalogue.Clefs.Select(ToDto).ToList(),
    };

    private static InstrumentFamilyInfoDto Family(FamilyInfo family) => new()
    {
        Id = (InstrumentFamilyDto)(int)family.Id,
        Label = family.Label,
        Stems = family.Stems,
        DetailNames = family.DetailNames,
        View = (InstrumentViewDto)(int)family.View,
    };

    private static TuningDto Tuning(Tuning tuning) => new()
    {
        Id = ToDto(tuning.Id),
        Instrument = tuning.Instrument,
        Label = tuning.Label,
        Pitches = tuning.Pitches,
    };

    private static WrittenInstrumentInfoDto Instrument(WrittenInstrumentInfo instrument) => new()
    {
        Id = ToDto(instrument.Id),
        Name = instrument.Name,
        KeyName = instrument.KeyName,
        Interval = instrument.Interval,
        DefaultClef = ClefName(instrument.Range.DefaultClef),
        LowMidi = instrument.Range.LowMidi,
        HighMidi = instrument.Range.HighMidi,
    };

    /// <summary>A tuning id.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The DTO (the enums share their order).</returns>
    public static TuningIdDto ToDto(TuningId id) => (TuningIdDto)(int)id;

    /// <summary>A written instrument.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The DTO (the enums share their order).</returns>
    public static WrittenInstrumentDto ToDto(WrittenInstrument id) => (WrittenInstrumentDto)(int)id;

    /// <summary>A clef choice.</summary>
    /// <param name="clef">The clef.</param>
    /// <returns>The DTO (the enums share their order).</returns>
    public static ClefChoiceDto ToDto(ClefChoice clef) => (ClefChoiceDto)(int)clef;

    /// <summary>The wire name of a clef choice.</summary>
    /// <param name="clef">The clef.</param>
    /// <returns><c>auto</c>, <c>treble</c>, ..., <c>bass-8vb</c>.</returns>
    public static string ClefName(ClefChoice clef) => ClefNames[(int)clef];
}

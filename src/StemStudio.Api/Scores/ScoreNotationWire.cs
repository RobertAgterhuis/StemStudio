using System.Text.Json;
using StemStudio.Api.InstrumentViews;
using StemStudio.Api.Worker;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Scores;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;

namespace StemStudio.Api.Scores;

/// <summary>
/// Maps the feature 016 score additions (tablature, part, chord symbols) between the domain, the backend 1.15.0 DTOs and the
/// worker 1.9.0 wire names. The domain and contract enums share their order; wire names come from the contract enums.
/// </summary>
public static class ScoreNotationWire
{
    /// <summary>The wire name of a contract enum value (<c>guitar-drop-d</c>, <c>alto-sax-eb</c>, <c>tab-only</c>).</summary>
    /// <typeparam name="TDto">A contract enum with string names.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The name.</returns>
    public static string Wire<TDto>(TDto value)
        where TDto : struct, Enum => JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(value))!;

    /// <summary>Reads a wire name.</summary>
    /// <typeparam name="TDto">A contract enum with string names.</typeparam>
    /// <param name="text">The name.</param>
    /// <returns>The value, or null for an unknown name.</returns>
    public static TDto? FromWire<TDto>(string? text)
        where TDto : struct, Enum
    {
        try
        {
            return text is null ? null : JsonSerializer.Deserialize<TDto>(JsonSerializer.Serialize(text));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The chord symbols of a request.</summary>
    /// <param name="chords">The symbols, or null.</param>
    /// <returns>The wire list, or null.</returns>
    public static List<WorkerScoreChord>? Chords(ScoreChords? chords) =>
        chords?.Symbols.Select(chord => new WorkerScoreChord(chord.StartMs, chord.Root, ChordQualities.HarteName(chord.Quality), chord.Bass)).ToList();

    /// <summary>Tablature settings on the wire.</summary>
    /// <param name="tablature">The settings, or null.</param>
    /// <returns>The wire settings, or null.</returns>
    public static WorkerTablatureSettings? ToWire(TablatureSettings? tablature) => tablature is null
        ? null
        : new WorkerTablatureSettings(Wire(CatalogueMapper.ToDto(tablature.Tuning)), tablature.Capo, Wire((TabLayoutDto)(int)tablature.Layout));

    /// <summary>Part settings on the wire.</summary>
    /// <param name="part">The settings, or null.</param>
    /// <returns>The wire settings, or null.</returns>
    public static WorkerPartSettings? ToWire(PartSettings? part) =>
        part is null ? null : new WorkerPartSettings(Wire(CatalogueMapper.ToDto(part.Instrument)), CatalogueMapper.ClefName(part.Clef));

    /// <summary>Tablature settings of a request DTO.</summary>
    /// <param name="dto">The DTO, or null.</param>
    /// <returns>The settings, or null.</returns>
    public static TablatureSettings? ToDomain(ScoreTablatureDto? dto) =>
        dto is null ? null : new TablatureSettings((TuningId)(int)dto.Tuning, dto.Capo, (TabLayout)(int)dto.Layout);

    /// <summary>Part settings of a request DTO.</summary>
    /// <param name="dto">The DTO, or null.</param>
    /// <returns>The settings, or null.</returns>
    public static PartSettings? ToDomain(ScorePartDto? dto) =>
        dto is null ? null : new PartSettings((WrittenInstrument)(int)dto.Instrument, (ClefChoice)(int)dto.Clef);

    /// <summary>The tablature DTO of a recipe.</summary>
    /// <param name="tablature">The settings, or null.</param>
    /// <returns>The DTO, or null.</returns>
    public static ScoreTablatureDto? ToDto(TablatureSettings? tablature) => tablature is null
        ? null
        : new ScoreTablatureDto { Tuning = CatalogueMapper.ToDto(tablature.Tuning), Capo = tablature.Capo, Layout = (TabLayoutDto)(int)tablature.Layout };

    /// <summary>The part DTO of a recipe.</summary>
    /// <param name="part">The settings, or null.</param>
    /// <returns>The DTO, or null.</returns>
    public static ScorePartDto? ToDto(PartSettings? part) =>
        part is null ? null : new ScorePartDto { Instrument = CatalogueMapper.ToDto(part.Instrument), Clef = CatalogueMapper.ToDto(part.Clef) };

    /// <summary>The tablature facts of a worker summary.</summary>
    /// <param name="summary">The wire facts, or null.</param>
    /// <returns>The facts, or null (also for unknown names).</returns>
    public static TablatureFacts? ToFacts(WorkerTablatureSummary? summary) =>
        summary is not null && FromWire<TuningIdDto>(summary.Tuning) is { } tuning && FromWire<TabLayoutDto>(summary.Layout) is { } layout
            ? new TablatureFacts
            {
                Tuning = (TuningId)(int)tuning,
                Strings = summary.Strings,
                Capo = summary.Capo,
                Layout = (TabLayout)(int)layout,
                OctaveMoved = summary.OctaveMoved,
                NotesDropped = summary.NotesDropped,
                WideStretches = summary.WideStretches,
            }
            : null;

    /// <summary>The part facts of a worker summary.</summary>
    /// <param name="summary">The wire facts, or null.</param>
    /// <returns>The facts, or null (also for unknown names).</returns>
    public static PartFacts? ToFacts(WorkerPartSummary? summary) =>
        summary is not null && FromWire<WrittenInstrumentDto>(summary.Instrument) is { } instrument && FromWire<ClefChoiceDto>(summary.Clef) is { } clef
            ? new PartFacts
            {
                Instrument = (WrittenInstrument)(int)instrument,
                Name = summary.Name,
                Interval = summary.Interval,
                WrittenKey = summary.WrittenKey,
                Clef = (ClefChoice)(int)clef,
                OutOfRange = summary.OutOfRange,
            }
            : null;

    /// <summary>The tablature facts DTO.</summary>
    /// <param name="facts">The facts, or null.</param>
    /// <returns>The DTO, or null.</returns>
    public static TablatureFactsDto? ToDto(TablatureFacts? facts) => facts is null
        ? null
        : new TablatureFactsDto
        {
            Tuning = CatalogueMapper.ToDto(facts.Tuning),
            Strings = facts.Strings,
            Capo = facts.Capo,
            Layout = (TabLayoutDto)(int)facts.Layout,
            OctaveMoved = facts.OctaveMoved,
            NotesDropped = facts.NotesDropped,
            WideStretches = facts.WideStretches,
        };

    /// <summary>The part facts DTO.</summary>
    /// <param name="facts">The facts, or null.</param>
    /// <returns>The DTO, or null.</returns>
    public static PartFactsDto? ToDto(PartFacts? facts) => facts is null
        ? null
        : new PartFactsDto
        {
            Instrument = CatalogueMapper.ToDto(facts.Instrument),
            Name = facts.Name,
            Interval = facts.Interval,
            WrittenKey = facts.WrittenKey,
            Clef = CatalogueMapper.ClefName(facts.Clef),
            OutOfRange = facts.OutOfRange,
        };
}

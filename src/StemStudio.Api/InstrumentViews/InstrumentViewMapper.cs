using StemStudio.Api.Tracks;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Scores;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;

namespace StemStudio.Api.InstrumentViews;

/// <summary>Maps the instrument view analyses to the backend 1.15.0 DTOs (data-model 7.5).</summary>
public static class InstrumentViewMapper
{
    /// <summary>Quality limits shown with the chord chart (FR-064).</summary>
    public const string ChordLimits = "Chord detection is a practice aid (about 80 % correct on pop songs); check by ear.";

    /// <summary>Quality limits shown with the drum hits (FR-064).</summary>
    public const string DrumHitLimits = "Drum hits are found in the split drum stems; bleed between pieces can add or hide a hit.";

    /// <summary>Quality limits shown with the reference melody (FR-064).</summary>
    public const string VocalPitchLimits = "The reference melody follows the lead vocal; breaths, effects and backing voices can make it jump.";

    private static readonly Dictionary<string, AnalysisEngineDto> Engines = new(StringComparer.Ordinal)
    {
        ["btc"] = AnalysisEngineDto.Btc,
        ["chroma-hmm"] = AnalysisEngineDto.ChromaHmm,
        ["drum-onsets"] = AnalysisEngineDto.DrumOnsets,
        ["pyin"] = AnalysisEngineDto.Pyin,
        ["yin"] = AnalysisEngineDto.Yin,
        ["fake"] = AnalysisEngineDto.Fake,
    };

    /// <summary>The chords analysis.</summary>
    /// <param name="view">The view.</param>
    /// <returns>The DTO (document only when ready or stale).</returns>
    public static TrackAnalysisDto<ChordChartDocumentDto> ToChordsDto(AnalysisView view) => ToDto(view, ChordLimits, (ChordChart chart) => ToDto(chart));

    /// <summary>The drum hits analysis.</summary>
    /// <param name="view">The view.</param>
    /// <returns>The DTO.</returns>
    public static TrackAnalysisDto<DrumHitsDocumentDto> ToDrumHitsDto(AnalysisView view) => ToDto(view, DrumHitLimits, (DrumHitsDocument hits) => new DrumHitsDocumentDto
    {
        Pieces = hits.Pieces.Select(ToDto).ToList(),
        Hits = hits.Hits.Select(hit => new DrumHitDto { TimeMs = hit.TimeMs, Piece = ToDto(hit.Piece), Strength = hit.Strength }).ToList(),
    });

    /// <summary>The reference melody analysis.</summary>
    /// <param name="view">The view.</param>
    /// <returns>The DTO.</returns>
    public static TrackAnalysisDto<VocalPitchDocumentDto> ToVocalPitchDto(AnalysisView view) => ToDto(view, VocalPitchLimits, (VocalPitchDocument pitch) => new VocalPitchDocumentDto
    {
        HopMs = pitch.HopMs,
        Frames = pitch.Frames,
        Source = pitch.Source == VocalSource.VocalsLead ? VocalSourceDto.VocalsLead : VocalSourceDto.Vocals,
        VoicedRatio = pitch.VoicedRatio,
        Range = pitch.Range is { } range ? new PitchRangeDto { LowCents = range.LowCents, HighCents = range.HighCents } : null,
        Warnings = ToDto(pitch.Warnings),
    });

    /// <summary>The chord chart document.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The DTO.</returns>
    public static ChordChartDocumentDto ToDto(ChordChart chart) => chart is null
        ? throw new ArgumentNullException(nameof(chart))
        : new ChordChartDocumentDto
        {
            Vocabulary = chart.Vocabulary == ChordVocabulary.Full ? ChordVocabularyDto.Full : ChordVocabularyDto.Simple,
            Engine = ChordEngine(chart.Engine),
            Key = chart.Key is { } key ? ToDto(key) : null,
            KeyRegions = chart.KeyRegions.Select(ToDto).ToList(),
            PreferFlats = chart.PreferFlats,
            BeatSnapped = chart.BeatSnapped,
            NoChords = chart.NoChords,
            HasEdits = chart.HasEdits,
            Segments = chart.Segments.Select(ToDto).ToList(),
            Warnings = ToDto(chart.Warnings),
        };

    /// <summary>A song key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The DTO.</returns>
    public static SongKeyDto ToDto(SongKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new SongKeyDto
        {
            Tonic = key.Tonic,
            Mode = ToDto(key.Mode),
            Confidence = key.Confidence,
            Alternative = key.Alternative is { } alternative ? new KeyAlternativeDto { Tonic = alternative.Tonic, Mode = ToDto(alternative.Mode) } : null,
        };
    }

    /// <summary>A key mode.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The DTO.</returns>
    public static KeyModeDto ToDto(KeyMode mode) => mode == KeyMode.Major ? KeyModeDto.Major : KeyModeDto.Minor;

    /// <summary>A chord quality.</summary>
    /// <param name="quality">The quality.</param>
    /// <returns>The DTO (the enums share their order).</returns>
    public static ChordQualityDto ToDto(ChordQuality quality) => (ChordQualityDto)(int)quality;

    /// <summary>A drum piece.</summary>
    /// <param name="piece">The piece.</param>
    /// <returns>The DTO (the enums share their order).</returns>
    public static DrumPieceDto ToDto(DrumPiece piece) => (DrumPieceDto)(int)piece;

    /// <summary>An analysis status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The DTO (the enums share their order).</returns>
    public static AnalysisStatusDto ToDto(AnalysisStatus status) => (AnalysisStatusDto)(int)status;

    private static TrackAnalysisDto<TDto> ToDto<TDocument, TDto>(AnalysisView view, string limits, Func<TDocument, TDto> document)
        where TDocument : AnalysisDocument
        where TDto : class
    {
        ArgumentNullException.ThrowIfNull(view);
        var stored = view.Stored;
        return new TrackAnalysisDto<TDto>
        {
            Kind = (AnalysisKindDto)(int)view.Kind,
            Status = ToDto(view.Status),
            Reason = view.Reason,
            Progress = stored is null || view.Status == AnalysisStatus.Unavailable ? 0 : stored.Progress,
            Engine = EngineOf(stored),
            Device = TrackMapper.ToDto(stored?.Device),
            EstimateSeconds = stored?.IsActive == true ? stored.EstimateSeconds : null,
            UpdatedAt = TrackMapper.Utc(stored?.UpdatedAt ?? view.Track.UpdatedAt),
            Limits = limits,
            Document = Shown(view, document),
        };
    }

    private static TDto? Shown<TDocument, TDto>(AnalysisView view, Func<TDocument, TDto> document)
        where TDocument : AnalysisDocument
        where TDto : class =>
        view.Status is AnalysisStatus.Ready or AnalysisStatus.Stale && view.Stored?.Document is TDocument typed ? document(typed) : null;

    private static AnalysisEngineDto? EngineOf(TrackAnalysis? stored) => stored?.Engine is { } engine && Engines.TryGetValue(engine, out var known) ? known : null;

    private static ChordEngineDto ChordEngine(string engine) => engine switch
    {
        "btc" => ChordEngineDto.Btc,
        "chroma-hmm" => ChordEngineDto.ChromaHmm,
        _ => ChordEngineDto.Fake,
    };

    private static KeyRegionDto ToDto(KeyRegion region) =>
        new() { StartMs = region.StartMs, EndMs = region.EndMs, Tonic = region.Tonic, Mode = ToDto(region.Mode), Confidence = region.Confidence };

    private static ChordSegmentDto ToDto(ChordSegment segment) => new()
    {
        StartMs = segment.StartMs,
        EndMs = segment.EndMs,
        Label = segment.Label,
        Root = segment.Root,
        Quality = segment.Quality is { } quality ? ToDto(quality) : null,
        Bass = segment.Bass,
        Confidence = segment.Confidence,
        Edited = segment.Edited,
    };

    private static List<AnalysisWarningDto> ToDto(IReadOnlyList<AnalysisWarning> warnings) =>
        warnings.Select(warning => new AnalysisWarningDto { Code = (AnalysisWarningCodeDto)(int)warning.Code, Text = warning.Text }).ToList();
}

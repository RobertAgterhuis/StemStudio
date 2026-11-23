using System.Text.Json.Serialization;

namespace StemStudio.Web.Audio;

/// <summary>One chord of a chart (<c>ChordSymbolEvent</c>, engine 2.6.0); <see cref="Root"/>/<see cref="Quality"/> null = no chord.</summary>
/// <param name="StartMs">Start in track milliseconds.</param>
/// <param name="EndMs">End in track milliseconds.</param>
/// <param name="Root">Root pitch class 0..11, or null.</param>
/// <param name="Quality">Quality wire name (<c>maj</c>, <c>min7</c>, ...), or null.</param>
/// <param name="Bass">Bass pitch class of a slash chord, or null.</param>
public sealed record ChordSymbolEvent(int StartMs, int EndMs, int? Root, string? Quality, int? Bass);

/// <summary>A key region (<c>KeyRegionData</c>).</summary>
/// <param name="StartMs">Start in track milliseconds.</param>
/// <param name="EndMs">End in track milliseconds.</param>
/// <param name="Tonic">Tonic pitch class 0..11.</param>
/// <param name="Mode"><c>major</c> or <c>minor</c>.</param>
public sealed record KeyRegionData(int StartMs, int EndMs, int Tonic, string Mode);

/// <summary>Argument of <c>attachChordLane</c> and <c>setKaraokeChords</c> (<c>ChordChartData</c>).</summary>
/// <param name="Segments">Contiguous, ordered segments.</param>
/// <param name="KeyTonic">Global key tonic, or null.</param>
/// <param name="KeyMode">Global key mode, or null.</param>
/// <param name="KeyRegions">Key regions (empty = the global key everywhere).</param>
public sealed record ChordChartData(IReadOnlyList<ChordSymbolEvent> Segments, int? KeyTonic, string? KeyMode, IReadOnlyList<KeyRegionData> KeyRegions);

/// <summary>Argument of <c>setChordDisplay</c> (capo mode, S12).</summary>
/// <param name="Capo">Capo 0..12.</param>
/// <param name="ShowSounding">Also show the sounding symbol (Advanced).</param>
public sealed record ChordDisplay(int Capo, bool ShowSounding);

/// <summary>Options of <c>attachChordLane</c>; unset values use the engine defaults.</summary>
public sealed record ChordLaneOptions
{
    /// <summary>Page instead of scrolling.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ReducedMotion { get; init; }
}

/// <summary>One drum hit (<c>DrumHitEvent</c>).</summary>
/// <param name="TimeMs">Track milliseconds.</param>
/// <param name="Piece">Piece wire name.</param>
/// <param name="Strength">Strength 0..1.</param>
public sealed record DrumHitEvent(int TimeMs, string Piece, double Strength);

/// <summary>Options of <c>attachHitLanes</c> and <c>setHitLaneOptions</c>; unset values use the engine defaults.</summary>
public sealed record HitLaneOptions
{
    /// <summary>Look-ahead 2000..8000 ms.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LookAheadMs { get; init; }

    /// <summary>Lanes to draw (piece wire names).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Pieces { get; init; }

    /// <summary>Page instead of scrolling.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ReducedMotion { get; init; }
}

/// <summary>Reference melody (<c>PitchContour</c>): 10 ms frames in centi-semitones, 0 = unvoiced.</summary>
/// <param name="Frames">The frames.</param>
public sealed record PitchContour(IReadOnlyList<int> Frames)
{
    /// <summary>Always 10.</summary>
    public int HopMs => 10;
}

/// <summary>One scored unit (<c>SingingUnit</c>).</summary>
/// <param name="Index">0-based index.</param>
/// <param name="StartMs">Start in track milliseconds.</param>
/// <param name="EndMs">End in track milliseconds.</param>
public sealed record SingingUnit(int Index, int StartMs, int EndMs);

/// <summary>What the singing scorer needs: the reference, the units and the tolerance (live and offline).</summary>
public abstract record ScoringRequest
{
    /// <summary>Reference melody.</summary>
    public required PitchContour Reference { get; init; }

    /// <summary>Units in time order.</summary>
    public required IReadOnlyList<SingingUnit> Units { get; init; }

    /// <summary>Tolerance 10..200 cents.</summary>
    public required int ToleranceCents { get; init; }
}

/// <summary>Argument of <c>startSingingFeedback</c>.</summary>
public sealed record SingingFeedbackRequest : ScoringRequest
{
    /// <summary>Host of the pitch view canvas.</summary>
    public required string PitchViewElementId { get; init; }

    /// <summary>Round trip of the stored 015 calibration, or null for the input's estimate.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? RoundTripMs { get; init; }

    /// <summary>The 015 <c>LatencyMethod</c> of <see cref="RoundTripMs"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LatencyMethod { get; init; }

    /// <summary>Page instead of scrolling.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ReducedMotion { get; init; }
}

/// <summary>The best unit of a summary.</summary>
/// <param name="Index">Unit index.</param>
/// <param name="Score">Its score.</param>
public sealed record SingingBest(int Index, int Score);

/// <summary>Summary of singing feedback or of a scored take (<c>SingingSummary</c>, never sent to the server).</summary>
public record SingingSummary
{
    /// <summary>Units with a score.</summary>
    public int UnitsScored { get; init; }

    /// <summary>Rounded mean, or null.</summary>
    public int? Average { get; init; }

    /// <summary>Best unit, or null.</summary>
    public SingingBest? Best { get; init; }

    /// <summary>Tolerance at the end.</summary>
    public int ToleranceCents { get; init; }

    /// <summary>Latency source (015 <c>LatencyMethod</c>).</summary>
    public string LatencySource { get; init; } = "estimated";
}

/// <summary>Score of one unit (<c>SingingUnitScore</c>).</summary>
/// <param name="Index">Unit index.</param>
/// <param name="Score">0..100, or null.</param>
/// <param name="Pass">1-based loop pass.</param>
public sealed record SingingUnitScore(int Index, int? Score, int Pass);

/// <summary>Result of <c>scoreTake</c>.</summary>
public sealed record TakeScoreResult : SingingSummary
{
    /// <summary>Scores of the units the take covers.</summary>
    public IReadOnlyList<SingingUnitScore> Units { get; init; } = [];
}

/// <summary>Argument of <c>scoreTake</c> (a 015 take).</summary>
public sealed record TakeScoreRequest : ScoringRequest
{
    /// <summary>Absolute audio URL of the take.</summary>
    public required string TakeUrl { get; init; }

    /// <summary>Song sample of the take's first sample.</summary>
    public required long StartSample { get; init; }

    /// <summary>
    /// Transpose (-12..12 semitones) the take was sung at, so the reference melody is compared in the same key (engine
    /// contract 2.6.0, default 0).
    /// </summary>
    public int TransposeSemitones { get; init; }
}

/// <summary>Result of <c>getRoundTripLatency</c>.</summary>
/// <param name="Ms">Round trip in milliseconds.</param>
/// <param name="Source">015 <c>LatencyMethod</c>.</param>
public sealed record RoundTripLatency(double Ms, string Source);

/// <summary>Arguments of the <c>OnSingingStateChanged</c> callback.</summary>
/// <param name="State"><c>idle</c>, <c>starting</c>, <c>running</c>, <c>stopped</c> or <c>failed</c>.</param>
/// <param name="Reason">Readable reason of <c>failed</c>, or null.</param>
public sealed record SingingStateChange(string State, string? Reason);

using StemStudio.Domain.InstrumentViews;

namespace StemStudio.Api.InstrumentViews;

/// <summary>Options of feature 016 (section <c>StemStudio:InstrumentViews</c>, data-model 1 and 7.7).</summary>
public sealed class InstrumentViewsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "StemStudio:InstrumentViews";

    /// <summary>Start chord detection when the standard separation completes (default true).</summary>
    public bool AutoChords { get; set; } = true;

    /// <summary>Start drum hit detection when the drums detail split completes (default true).</summary>
    public bool AutoDrumHits { get; set; } = true;

    /// <summary>Seconds per audio second of the shown estimates.</summary>
    public AnalysisEstimateFactors Estimates { get; set; } = new();

    /// <summary>The switches of the auto rules.</summary>
    public AutoRuleSettings AutoRules => new(AutoChords, AutoDrumHits);
}

/// <summary>Estimate factors per kind, engine and device in seconds per audio second (data-model 7.7 defaults).</summary>
public sealed class AnalysisEstimateFactors
{
    /// <summary>Chords with BTC on the CPU.</summary>
    public double ChordsBtcCpu { get; set; } = 0.03;

    /// <summary>Chords with BTC on a GPU.</summary>
    public double ChordsBtcGpu { get; set; } = 0.01;

    /// <summary>Chords with the chroma HMM.</summary>
    public double ChordsChromaHmm { get; set; } = 0.015;

    /// <summary>Drum hits.</summary>
    public double DrumHits { get; set; } = 0.02;

    /// <summary>Reference melody with pYIN.</summary>
    public double VocalPitchPyin { get; set; } = 0.035;

    /// <summary>Reference melody with YIN.</summary>
    public double VocalPitchYin { get; set; } = 0.005;

    /// <summary>The factor of a run.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="engine">The engine that will run (<c>btc</c>, <c>chroma-hmm</c>, <c>pyin</c>, <c>yin</c>, ...).</param>
    /// <param name="gpu">The worker runs on a GPU.</param>
    /// <returns>Seconds per audio second.</returns>
    public double For(AnalysisKind kind, string engine, bool gpu) => (kind, engine) switch
    {
        (AnalysisKind.Chords, "chroma-hmm") => ChordsChromaHmm,
        (AnalysisKind.Chords, _) => gpu ? ChordsBtcGpu : ChordsBtcCpu,
        (AnalysisKind.DrumHits, _) => DrumHits,
        (_, "yin") => VocalPitchYin,
        _ => VocalPitchPyin,
    };
}

namespace StemStudio.Contracts.InstrumentViews;

/// <summary><c>Track.instrumentViews</c>: the status of each analysis (backend-api 1.15.0, feature 016).</summary>
public sealed record InstrumentViewsStatusDto
{
    /// <summary>Chord chart status.</summary>
    public required AnalysisStatusDto Chords { get; init; }

    /// <summary>Drum hit map status.</summary>
    public required AnalysisStatusDto DrumHits { get; init; }

    /// <summary>Reference melody status.</summary>
    public required AnalysisStatusDto VocalPitch { get; init; }

    /// <summary>Every analysis <c>none</c> (older tracks; also the meaning of an absent value).</summary>
    public static InstrumentViewsStatusDto None { get; } =
        new() { Chords = AnalysisStatusDto.None, DrumHits = AnalysisStatusDto.None, VocalPitch = AnalysisStatusDto.None };
}

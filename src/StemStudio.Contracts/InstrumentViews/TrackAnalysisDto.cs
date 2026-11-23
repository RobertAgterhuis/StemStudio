using StemStudio.Contracts.Tracks;

namespace StemStudio.Contracts.InstrumentViews;

/// <summary>One per-track analysis with its document (<c>ChordsAnalysis</c>, <c>DrumHitsAnalysis</c>, <c>VocalPitchAnalysis</c>) (backend-api 1.15.0, feature 016).</summary>
/// <typeparam name="TDocument">The document type.</typeparam>
public sealed record TrackAnalysisDto<TDocument>
    where TDocument : class
{
    /// <summary>Kind.</summary>
    public required AnalysisKindDto Kind { get; init; }

    /// <summary>Status.</summary>
    public required AnalysisStatusDto Status { get; init; }

    /// <summary>Readable reason for failed, unavailable and stale.</summary>
    public required string? Reason { get; init; }

    /// <summary>Progress 0..1.</summary>
    public required double Progress { get; init; }

    /// <summary>Engine of the document, or null.</summary>
    public required AnalysisEngineDto? Engine { get; init; }

    /// <summary>Where the model ran (BTC), or null.</summary>
    public required JobDeviceDto? Device { get; init; }

    /// <summary>Estimate in seconds while queued or running, else null.</summary>
    public required int? EstimateSeconds { get; init; }

    /// <summary>Last change.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Quality limits shown with the result (FR-064, at most 300 characters).</summary>
    public required string Limits { get; init; }

    /// <summary>The document; null unless ready or stale.</summary>
    public required TDocument? Document { get; init; }
}

using StemStudio.Contracts.Tracks;

namespace StemStudio.Contracts.Scores;

/// <summary>Facts of a ready score item: the worker's summary plus the device (backend contract 1.7.0).</summary>
public sealed record ScoreFactsDto
{
    /// <summary>Profile.</summary>
    public required ScoreProfileDto Profile { get; init; }

    /// <summary>Engine.</summary>
    public required ScoreEngineDto Engine { get; init; }

    /// <summary>Piano quality; null for other profiles.</summary>
    public ScoreQualityDto? Quality { get; init; }

    /// <summary>Key; null for drums.</summary>
    public KeyInfoDto? Key { get; init; }

    /// <summary>Rounded tempo of the tempo mark.</summary>
    public required int Bpm { get; init; }

    /// <summary>Time signature, e.g. <c>4/4</c>.</summary>
    public required string TimeSignature { get; init; }

    /// <summary>Measures (with a pickup bar).</summary>
    public required int Bars { get; init; }

    /// <summary>Notes or drum hits in the score.</summary>
    public required int Notes { get; init; }

    /// <summary>PDF pages.</summary>
    public required int Pages { get; init; }

    /// <summary>Resolution.</summary>
    public required ScoreResolutionDto Resolution { get; init; }

    /// <summary>Warnings (at most 8).</summary>
    public IReadOnlyList<ScoreWarningDto> Warnings { get; init; } = [];

    /// <summary>Model credit (piano model, CC BY 4.0), or null.</summary>
    public string? Attribution { get; init; }

    /// <summary>Where the model ran.</summary>
    public JobDeviceDto? Device { get; init; }
}

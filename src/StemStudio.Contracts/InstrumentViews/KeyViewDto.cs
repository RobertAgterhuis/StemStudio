namespace StemStudio.Contracts.InstrumentViews;

/// <summary><c>GET /api/tracks/{trackId}/key</c> (<c>KeyView</c>) (backend-api 1.15.0, feature 016).</summary>
public sealed record KeyViewDto
{
    /// <summary>Status of the chords analysis.</summary>
    public required AnalysisStatusDto Status { get; init; }

    /// <summary>Requested transpose (-12..12).</summary>
    public required int Transpose { get; init; }

    /// <summary>The global key; null unless chords are ready.</summary>
    public required SongKeyDto? Key { get; init; }

    /// <summary>Display, e.g. "D major (original C major, +2)".</summary>
    public required string? Display { get; init; }

    /// <summary>Written key of the requested instrument; null for concert instruments.</summary>
    public required string? WrittenKey { get; init; }

    /// <summary>Key regions (0..50).</summary>
    public required IReadOnlyList<KeyViewRegionDto> Regions { get; init; }
}

using StemStudio.Contracts.Styles;

namespace StemStudio.Contracts.Generation;

/// <summary>Body of <c>POST /api/tracks/{trackId}/restyles</c> (1.8.0); null members take the defaults of the contract.</summary>
public sealed record RestyleRequest
{
    /// <summary>The style to apply.</summary>
    public required Guid StyleId { get; init; }

    /// <summary>Range or in-place.</summary>
    public required RestyleModeDto Mode { get; init; }

    /// <summary>Range start (default 0).</summary>
    public double? RangeStartSeconds { get; init; }

    /// <summary>Range end (default min(duration, start + 480)).</summary>
    public double? RangeEndSeconds { get; init; }

    /// <summary>Stems to include (default all; vocals excluded for an instrumental style).</summary>
    public IReadOnlyList<string>? IncludedStems { get; init; }

    /// <summary>Strength (default the style's).</summary>
    public StrengthDto? Strength { get; init; }

    /// <summary>Number of takes, 1-2.</summary>
    public int Takes { get; init; } = 1;
}

namespace StemStudio.Contracts.ScoreFollow;

/// <summary>One SVG page of a score view (<c>ScoreViewPage</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record ScoreViewPageDto
{
    /// <summary>1-based page number.</summary>
    public required int Page { get; init; }

    /// <summary>API-relative image URL with <c>?v=</c> (served as <c>image/svg+xml</c>).</summary>
    public required string Url { get; init; }

    /// <summary>Page width in CSS pixels.</summary>
    public required int WidthPx { get; init; }

    /// <summary>Page height in CSS pixels.</summary>
    public required int HeightPx { get; init; }
}

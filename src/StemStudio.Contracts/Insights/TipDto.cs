namespace StemStudio.Contracts.Insights;

/// <summary>A plain-language tip (backend-api 1.19.0, feature 020).</summary>
public sealed record TipDto
{
    /// <summary>Tip code.</summary>
    public required TipCodeDto Code { get; init; }

    /// <summary>Tip text (at most 300 characters).</summary>
    public required string Text { get; init; }

    /// <summary>The button's action; null for advice without an action.</summary>
    public required TipActionDto? Action { get; init; }
}

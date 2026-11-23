namespace StemStudio.Contracts.Insights;

/// <summary>The action of a tip (backend-api 1.19.0, feature 020).</summary>
public sealed record TipActionDto
{
    /// <summary>What the button starts.</summary>
    public required TipActionKindDto Kind { get; init; }

    /// <summary>ChannelId of the stem the tip is about (<c>vocals</c>, <c>drums.kick</c>).</summary>
    public required string Channel { get; init; }

    /// <summary>Parent stem for detail actions; null otherwise.</summary>
    public required string? Parent { get; init; }

    /// <summary>Detail stem name for <c>open-detail</c>; null otherwise.</summary>
    public required string? Child { get; init; }
}

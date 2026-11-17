namespace StemStudio.Contracts.LyricAssist;

/// <summary>Where a suggestion applies (schema <c>Anchor</c>).</summary>
public sealed record AnchorDto
{
    /// <summary>1-based line in the normalised text; 0 for titles.</summary>
    public required int Line { get; init; }

    /// <summary>UTF-16 offset within the line.</summary>
    public required int Start { get; init; }

    /// <summary>Length in UTF-16 code units (0 for an insertion point).</summary>
    public required int Length { get; init; }

    /// <summary>The text at the anchor (at most 400 characters); verified on every use.</summary>
    public required string Original { get; init; }
}

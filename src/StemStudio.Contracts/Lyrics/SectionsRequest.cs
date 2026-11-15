namespace StemStudio.Contracts.Lyrics;

/// <summary>Body of <c>POST /api/lyrics/sections</c> (1.9.0).</summary>
public sealed record SectionsRequest
{
    /// <summary>The lyrics text.</summary>
    public required string Text { get; init; }
}

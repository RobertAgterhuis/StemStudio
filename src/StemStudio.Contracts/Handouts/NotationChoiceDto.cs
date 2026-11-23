namespace StemStudio.Contracts.Handouts;

/// <summary>A notation item that can go into a handout (backend-api 1.19.0, feature 020).</summary>
public sealed record NotationChoiceDto
{
    /// <summary>Ready <c>score</c> library item.</summary>
    public required Guid AssetId { get; init; }

    /// <summary>Title (at most 120 characters).</summary>
    public required string Title { get; init; }

    /// <summary>Kind.</summary>
    public required NotationKindDto Kind { get; init; }

    /// <summary>Transposition of a transposed part; null otherwise.</summary>
    public required TranspositionDto? Transposition { get; init; }

    /// <summary>Selected by default.</summary>
    public required bool DefaultSelected { get; init; }

    /// <summary>Song it belongs to (at most 300 characters).</summary>
    public required string SongTitle { get; init; }
}

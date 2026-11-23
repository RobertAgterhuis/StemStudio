namespace StemStudio.Contracts.Handouts;

/// <summary>Body of <c>createHandout</c> (backend-api 1.19.0, feature 020).</summary>
public sealed record CreateHandoutRequest
{
    /// <summary>Song or setlist.</summary>
    public required HandoutSourceDto Source { get; init; }

    /// <summary>Family.</summary>
    public required HandoutFamilyDto Family { get; init; }

    /// <summary>Ingredients; null = the family defaults (data-model 6.2).</summary>
    public IReadOnlyList<IngredientCodeDto>? Include { get; init; }

    /// <summary>Notation items; null = the default notation per song.</summary>
    public IReadOnlyList<Guid>? NotationAssetIds { get; init; }

    /// <summary>Transposition (<c>melodic</c> only); null = <c>c</c>.</summary>
    public TranspositionDto? Transposition { get; init; }

    /// <summary>Paper; null = the default.</summary>
    public PaperDto? Paper { get; init; }

    /// <summary>Item name (1..120 characters); null = the default name.</summary>
    public string? Name { get; init; }
}

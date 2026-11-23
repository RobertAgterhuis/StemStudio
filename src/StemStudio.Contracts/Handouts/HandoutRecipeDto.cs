namespace StemStudio.Contracts.Handouts;

/// <summary><c>Provenance.handoutRecipe</c>: posting it as a <see cref="CreateHandoutRequest"/> makes the handout again (backend-api 1.19.0, feature 020).</summary>
public sealed record HandoutRecipeDto
{
    /// <summary>Always <see cref="RecipeKind"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>Song or setlist.</summary>
    public required HandoutKindDto KindLabel { get; init; }

    /// <summary>Song; null for a setlist handout.</summary>
    public required Guid? TrackId { get; init; }

    /// <summary>Setlist; null for a song handout.</summary>
    public required Guid? SetlistId { get; init; }

    /// <summary>Setlist name snapshot; null for a song handout.</summary>
    public required string? SetlistName { get; init; }

    /// <summary>Family.</summary>
    public required HandoutFamilyDto Family { get; init; }

    /// <summary>Ingredients; null = defaults.</summary>
    public required IReadOnlyList<IngredientCodeDto>? Include { get; init; }

    /// <summary>Notation items; null = defaults.</summary>
    public required IReadOnlyList<Guid>? NotationAssetIds { get; init; }

    /// <summary>Transposition; null when none.</summary>
    public required TranspositionDto? Transposition { get; init; }

    /// <summary>Paper.</summary>
    public required PaperDto Paper { get; init; }

    /// <summary>The constant <c>kind</c> value.</summary>
    public const string RecipeKind = "handout";
}

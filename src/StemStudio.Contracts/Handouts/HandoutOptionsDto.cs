namespace StemStudio.Contracts.Handouts;

/// <summary>Families, ingredients and notation choices for a song or setlist (<c>getHandoutOptions</c>) (backend-api 1.19.0, feature 020).</summary>
public sealed record HandoutOptionsDto
{
    /// <summary>Song or setlist.</summary>
    public required HandoutKindDto Kind { get; init; }

    /// <summary>Family.</summary>
    public required HandoutFamilyDto Family { get; init; }

    /// <summary>Display label of the family (at most 60 characters).</summary>
    public required string FamilyLabel { get; init; }

    /// <summary>Number of songs.</summary>
    public required int Songs { get; init; }

    /// <summary>Ingredients with status.</summary>
    public required IReadOnlyList<IngredientDto> Ingredients { get; init; }

    /// <summary>Notation choices.</summary>
    public required IReadOnlyList<NotationChoiceDto> Notation { get; init; }

    /// <summary>Offered transpositions (<c>melodic</c> only; empty otherwise).</summary>
    public required IReadOnlyList<TranspositionDto> Transpositions { get; init; }

    /// <summary>Default paper.</summary>
    public required PaperDto DefaultPaper { get; init; }

    /// <summary>Estimated time to make the handout.</summary>
    public required int EstimateSeconds { get; init; }
}

namespace StemStudio.Contracts.Handouts;

/// <summary><c>Asset.handout</c>: facts of a ready handout item (backend-api 1.19.0, feature 020).</summary>
public sealed record HandoutFactsDto
{
    /// <summary>Song or setlist.</summary>
    public required HandoutKindDto Kind { get; init; }

    /// <summary>Family.</summary>
    public required HandoutFamilyDto Family { get; init; }

    /// <summary>Transposition; null when none.</summary>
    public required TranspositionDto? Transposition { get; init; }

    /// <summary>Paper.</summary>
    public required PaperDto Paper { get; init; }

    /// <summary>Page count.</summary>
    public required int Pages { get; init; }

    /// <summary>Number of songs (1..50).</summary>
    public required int Songs { get; init; }

    /// <summary>Ingredients in the PDF.</summary>
    public required IReadOnlyList<IngredientCodeDto> Included { get; init; }

    /// <summary>Ingredients left out, with reasons.</summary>
    public required IReadOnlyList<OmittedIngredientDto> Omitted { get; init; }

    /// <summary>Renderer warnings.</summary>
    public required IReadOnlyList<HandoutWarningDto> Warnings { get; init; }

    /// <summary>Bookmark titles in order.</summary>
    public required IReadOnlyList<string> Outline { get; init; }
}

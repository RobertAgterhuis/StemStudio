namespace StemStudio.Contracts.Handouts;

/// <summary>An ingredient left out of a handout (backend-api 1.19.0, feature 020).</summary>
public sealed record OmittedIngredientDto
{
    /// <summary>Ingredient.</summary>
    public required IngredientCodeDto Code { get; init; }

    /// <summary>Why.</summary>
    public required OmissionReasonDto Reason { get; init; }
}

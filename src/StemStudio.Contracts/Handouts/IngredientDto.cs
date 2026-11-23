namespace StemStudio.Contracts.Handouts;

/// <summary>An ingredient offered for a handout (backend-api 1.19.0, feature 020).</summary>
public sealed record IngredientDto
{
    /// <summary>Ingredient.</summary>
    public required IngredientCodeDto Code { get; init; }

    /// <summary>Status.</summary>
    public required IngredientStatusDto Status { get; init; }

    /// <summary>Included by the one-click default.</summary>
    public required bool DefaultIncluded { get; init; }

    /// <summary>Short description (at most 200 characters).</summary>
    public required string Text { get; init; }

    /// <summary>Create action for a missing ingredient; null otherwise.</summary>
    public required IngredientActionDto? Action { get; init; }
}

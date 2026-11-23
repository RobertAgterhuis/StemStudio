#pragma warning disable IDE0005, CS9113, CS1998, IDE0060, CA1822, CA1801, IDE0051, IDE0052, CS0169, CS0414
namespace StemStudio.Domain.Handouts;

/// <summary>Whether a song has the data of an ingredient.</summary>
public enum IngredientAvailability
{
    /// <summary>Available.</summary>
    Available,

    /// <summary>The song has no data for it.</summary>
    Missing,

    /// <summary>The data is there but has no timing (unsynced lyrics, no reliable beat grid).</summary>
    NoTiming,
}

/// <summary>What goes into a handout: the printed ingredients and the omitted ones with their reason.</summary>
/// <param name="Included">Printed ingredients, in print order.</param>
/// <param name="Omitted">Offered ingredients left out.</param>
public sealed record IngredientPlan(IReadOnlyList<IngredientCode> Included, IReadOnlyList<OmittedIngredient> Omitted);

/// <summary>
/// Plans the ingredients of a handout (data-model 6.2, FR-008, FR-010): the request (or the family defaults) filtered by what
/// the songs have; a missing optional ingredient never fails the request, it is listed with its reason.
/// </summary>
public static class IngredientPlanner
{
    /// <summary>The codes of a request that the family does not offer (a 422 for each).</summary>
    /// <param name="family">Family.</param>
    /// <param name="kind">Song or setlist.</param>
    /// <param name="include">The requested codes, or null for the defaults.</param>
    /// <returns>One message per refused code.</returns>
    public static IReadOnlyList<string> Refused(HandoutFamily family, HandoutKind kind, IReadOnlyList<IngredientCode>? include) => throw new NotImplementedException();

    /// <summary>Plans a handout.</summary>
    /// <param name="family">Family.</param>
    /// <param name="kind">Song or setlist.</param>
    /// <param name="include">The requested codes (offered ones only), or null for the defaults.</param>
    /// <param name="availability">Availability of each code over the songs (absent = missing).</param>
    /// <returns>The plan.</returns>
    public static IngredientPlan Plan(HandoutFamily family, HandoutKind kind, IReadOnlyList<IngredientCode>? include, IReadOnlyDictionary<IngredientCode, IngredientAvailability> availability)
    {
        throw new NotImplementedException();
    }

    /// <summary>The one-click defaults of a family.</summary>
    /// <param name="family">Family.</param>
    /// <param name="kind">Song or setlist.</param>
    /// <returns>The default codes.</returns>
    public static IReadOnlyList<IngredientCode> Defaults(HandoutFamily family, HandoutKind kind) => throw new NotImplementedException();

    /// <summary>The wire value of an enum (kebab-case).</summary>
    /// <typeparam name="TEnum">Enum type.</typeparam>
    /// <param name="value">Value.</param>
    /// <returns>The wire text.</returns>
    public static string Wire<TEnum>(TEnum value)
        where TEnum : struct, Enum => throw new NotImplementedException();
}

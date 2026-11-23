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
    public static IReadOnlyList<string> Refused(HandoutFamily family, HandoutKind kind, IReadOnlyList<IngredientCode>? include) =>
        (include ?? []).Distinct().Where(code => Families.Offer(family, code, kind) == IngredientOffer.No)
            .Select(code => $"{Wire(code)} is not offered for the {Wire(family)} family.").ToList();

    /// <summary>Plans a handout.</summary>
    /// <param name="family">Family.</param>
    /// <param name="kind">Song or setlist.</param>
    /// <param name="include">The requested codes (offered ones only), or null for the defaults.</param>
    /// <param name="availability">Availability of each code over the songs (absent = missing).</param>
    /// <returns>The plan.</returns>
    public static IngredientPlan Plan(HandoutFamily family, HandoutKind kind, IReadOnlyList<IngredientCode>? include, IReadOnlyDictionary<IngredientCode, IngredientAvailability> availability)
    {
        ArgumentNullException.ThrowIfNull(availability);
        var wanted = (include ?? Defaults(family, kind)).ToHashSet();
        var included = new List<IngredientCode>();
        var omitted = new List<OmittedIngredient>();
        foreach (var code in Families.Offered(family, kind))
        {
            var state = availability.GetValueOrDefault(code, IngredientAvailability.Missing);
            if (!wanted.Contains(code))
            {
                omitted.Add(new OmittedIngredient(code, OmissionReason.NotSelected));
            }
            else if (state == IngredientAvailability.Available)
            {
                included.Add(code);
            }
            else
            {
                omitted.Add(new OmittedIngredient(code, state == IngredientAvailability.NoTiming ? OmissionReason.NoTiming : OmissionReason.NotAvailable));
            }
        }

        return new IngredientPlan(included, omitted);
    }

    /// <summary>The one-click defaults of a family.</summary>
    /// <param name="family">Family.</param>
    /// <param name="kind">Song or setlist.</param>
    /// <returns>The default codes.</returns>
    public static IReadOnlyList<IngredientCode> Defaults(HandoutFamily family, HandoutKind kind) =>
        Families.Offered(family, kind).Where(code => Families.Offer(family, code, kind) == IngredientOffer.Default).ToList();

    /// <summary>The wire value of an enum (kebab-case).</summary>
    /// <typeparam name="TEnum">Enum type.</typeparam>
    /// <param name="value">Value.</param>
    /// <returns>The wire text.</returns>
    public static string Wire<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        string.Concat(value.ToString().Select((character, index) => char.IsUpper(character) && index > 0 ? "-" + char.ToLowerInvariant(character) : char.ToLowerInvariant(character).ToString()));
}

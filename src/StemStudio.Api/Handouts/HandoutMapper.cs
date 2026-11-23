#pragma warning disable IDE0005, CS9113, CS1998, IDE0060, CA1822, CA1801, IDE0051, IDE0052, CS0169, CS0414
using StemStudio.Contracts.Handouts;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Handouts;

namespace StemStudio.Api.Handouts;

/// <summary>Maps handout values between the contract (backend 1.19.0) and the domain; enum members share their names.</summary>
public static class HandoutMapper
{
    /// <summary>A contract enum to its domain twin (same member names).</summary>
    /// <typeparam name="TTo">Target enum.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The converted value.</returns>
    public static TTo Convert<TTo>(Enum value)
        where TTo : struct, Enum
    {
        throw new NotImplementedException();
    }

    /// <summary>The recipe of a request (defaults applied: paper a4).</summary>
    /// <param name="request">A request whose source has exactly one id.</param>
    /// <returns>The recipe.</returns>
    public static HandoutRecipe ToRecipe(CreateHandoutRequest request)
    {
        throw new NotImplementedException();
    }

    /// <summary><c>Asset.handout</c>: set only when the item is ready (backend 1.19.0).</summary>
    /// <param name="asset">The item.</param>
    /// <returns>The facts or null.</returns>
    public static HandoutFactsDto? ToFactsDto(Asset asset)
    {
        throw new NotImplementedException();
    }

    /// <summary><c>Provenance.handoutRecipe</c>; null for other origins.</summary>
    /// <param name="recipe">The recipe, or null.</param>
    /// <returns>The DTO or null.</returns>
    public static HandoutRecipeDto? ToRecipeDto(HandoutRecipe? recipe) => throw new NotImplementedException();
}

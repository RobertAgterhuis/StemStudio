using StemStudio.Contracts.Handouts;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Handouts;

namespace StemStudio.Api.Handouts;

/// <summary>Maps handout values between the contract (backend 1.19.0) and the domain; enum members share their names.</summary>
public static class HandoutMapper
{
    /// <summary>A contract enum to its domain twin (same member names); an undefined value is never converted.</summary>
    /// <typeparam name="TTo">Target enum.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The converted value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined member.</exception>
    public static TTo Convert<TTo>(Enum value)
        where TTo : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(value);
        return Enum.GetName(value.GetType(), value) is { } name
            ? Enum.Parse<TTo>(name)
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Not a defined member.");
    }

    /// <summary>The recipe of a request (defaults applied: paper a4).</summary>
    /// <param name="request">A request whose source has exactly one id.</param>
    /// <returns>The recipe.</returns>
    public static HandoutRecipe ToRecipe(CreateHandoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new HandoutRecipe
        {
            Kind = request.Source.TrackId is not null ? HandoutKind.Song : HandoutKind.Setlist,
            TrackId = request.Source.TrackId,
            SetlistId = request.Source.SetlistId,
            Family = Convert<HandoutFamily>(request.Family),
            Include = request.Include?.Select(code => Convert<IngredientCode>(code)).Distinct().ToList(),
            NotationAssetIds = request.NotationAssetIds?.Distinct().ToList(),
            Transposition = request.Transposition is { } transposition ? Convert<Transposition>(transposition) : null,
            Paper = request.Paper is { } paper ? Convert<Paper>(paper) : Paper.A4,
        };
    }

    /// <summary><c>Asset.handout</c>: set only when the item is ready (backend 1.19.0).</summary>
    /// <param name="asset">The item.</param>
    /// <returns>The facts or null.</returns>
    public static HandoutFactsDto? ToFactsDto(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (asset is not { Status: AssetStatus.Ready, Handout: { } facts })
        {
            return null;
        }

        return new HandoutFactsDto
        {
            Kind = Convert<HandoutKindDto>(facts.Kind),
            Family = Convert<HandoutFamilyDto>(facts.Family),
            Transposition = facts.Transposition is { } transposition ? Convert<TranspositionDto>(transposition) : null,
            Paper = Convert<PaperDto>(facts.Paper),
            Pages = facts.Pages,
            Songs = facts.Songs,
            Included = facts.Included.Select(code => Convert<IngredientCodeDto>(code)).ToList(),
            Omitted = facts.Omitted.Select(item => new OmittedIngredientDto { Code = Convert<IngredientCodeDto>(item.Code), Reason = Convert<OmissionReasonDto>(item.Reason) }).ToList(),
            Warnings = facts.Warnings.Select(item => new HandoutWarningDto { Code = Convert<HandoutWarningCodeDto>(item.Code), Text = item.Text }).ToList(),
            Outline = facts.Outline,
        };
    }

    /// <summary><c>Provenance.handoutRecipe</c>; null for other origins.</summary>
    /// <param name="recipe">The recipe, or null.</param>
    /// <returns>The DTO or null.</returns>
    public static HandoutRecipeDto? ToRecipeDto(HandoutRecipe? recipe) => recipe is null ? null : new HandoutRecipeDto
    {
        Kind = HandoutRecipeDto.RecipeKind,
        KindLabel = Convert<HandoutKindDto>(recipe.Kind),
        TrackId = recipe.TrackId,
        SetlistId = recipe.SetlistId,
        SetlistName = recipe.SetlistName,
        Family = Convert<HandoutFamilyDto>(recipe.Family),
        Include = recipe.Include?.Select(code => Convert<IngredientCodeDto>(code)).ToList(),
        NotationAssetIds = recipe.NotationAssetIds,
        Transposition = recipe.Transposition is { } transposition ? Convert<TranspositionDto>(transposition) : null,
        Paper = Convert<PaperDto>(recipe.Paper),
    };
}

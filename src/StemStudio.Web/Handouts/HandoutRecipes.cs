using StemStudio.Contracts.Handouts;

namespace StemStudio.Web.Handouts;

/// <summary>"Make again" (FR-014): a handout recipe posted unchanged as a <c>createHandout</c> body.</summary>
public static class HandoutRecipes
{
    /// <summary>The request of a recipe.</summary>
    /// <param name="recipe">The item's recipe.</param>
    /// <returns>The request.</returns>
    public static CreateHandoutRequest Request(HandoutRecipeDto recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return new()
        {
            Source = recipe.SetlistId is { } setlist ? HandoutSourceDto.Setlist(setlist) : HandoutSourceDto.Song(recipe.TrackId!.Value),
            Family = recipe.Family,
            Include = recipe.Include,
            NotationAssetIds = recipe.NotationAssetIds,
            Transposition = recipe.Transposition,
            Paper = recipe.Paper,
        };
    }
}

using StemStudio.Contracts.Handouts;

namespace StemStudio.Web.Handouts;

/// <summary>Where the create link of a missing ingredient goes (020 Q3: existing flows only).</summary>
/// <param name="ScoreStem">Open the sheet music dialog for this stem; null otherwise.</param>
/// <param name="Url">Navigate to this relative URL (a card of the track page or the mixer); null otherwise.</param>
/// <param name="Notes">Select the family in the practice notes editor.</param>
public sealed record IngredientTarget(string? ScoreStem, string? Url, bool Notes);

/// <summary>A create link chosen in the handout dialog.</summary>
/// <param name="Action">The ingredient action.</param>
/// <param name="Family">The family of the dialog.</param>
public sealed record IngredientRequest(IngredientActionDto Action, HandoutFamilyDto Family);

/// <summary>A handout item that was just created.</summary>
/// <param name="Asset">The new library item (queued).</param>
/// <param name="Family">Its family.</param>
public sealed record HandoutCreated(StemStudio.Contracts.Assets.AssetDto Asset, HandoutFamilyDto Family);

/// <summary>Maps the create links of missing ingredients to the existing flows of the track page.</summary>
public static class IngredientLinks
{
    /// <summary>The target of an ingredient action.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="action">The action.</param>
    /// <param name="family">The family of the handout.</param>
    /// <returns>The target.</returns>
    public static IngredientTarget Target(Guid trackId, IngredientActionDto action, HandoutFamilyDto family) => action switch
    {
        IngredientActionDto.CreateScore or IngredientActionDto.CreateTab or IngredientActionDto.CreateDrumNotation or IngredientActionDto.CreateTransposedPart
            => new IngredientTarget(StemOf(family), null, false),
        IngredientActionDto.DetectChords or IngredientActionDto.FindLyrics => new IngredientTarget(null, $"tracks/{trackId}#lyrics-heading", false),
        IngredientActionDto.SplitDrums => new IngredientTarget(null, $"tracks/{trackId}#detail-heading", false),
        IngredientActionDto.DetectSections => new IngredientTarget(null, $"tracks/{trackId}/mixer", false),
        _ => new IngredientTarget(null, null, true),
    };

    /// <summary>The standard stem whose sheet music a family uses (melodic instruments and the overview: <c>other</c>).</summary>
    /// <param name="family">The family.</param>
    /// <returns>The stem name.</returns>
    public static string StemOf(HandoutFamilyDto family) => family switch
    {
        HandoutFamilyDto.Vocals => "vocals",
        HandoutFamilyDto.Guitar => "guitar",
        HandoutFamilyDto.Bass => "bass",
        HandoutFamilyDto.Keys => "piano",
        HandoutFamilyDto.Drums => "drums",
        _ => "other",
    };
}

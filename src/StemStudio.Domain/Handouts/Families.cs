using StemStudio.Domain.Scores;

namespace StemStudio.Domain.Handouts;

/// <summary>Whether a family offers an ingredient and whether the one-click handout includes it (data-model 6.2).</summary>
public enum IngredientOffer
{
    /// <summary>Not offered (<c>-</c>).</summary>
    No,

    /// <summary>Optional (Advanced dialog).</summary>
    Optional,

    /// <summary>Included by default when available.</summary>
    Default,
}

/// <summary>
/// The seven equal handout families (data-model 6.1, 6.2): labels, the ingredients each offers and includes by default, and
/// which notation fits. Every family gets facts, a form chart, notation or chords, and notes (no family gets less).
/// </summary>
public static class Families
{
    private const IngredientOffer D = IngredientOffer.Default;
    private const IngredientOffer O = IngredientOffer.Optional;
    private const IngredientOffer N = IngredientOffer.No;

    /// <summary>The song ingredients in print order (<see cref="IngredientCode.SetlistNotes"/> is added for setlists).</summary>
    public static readonly IReadOnlyList<IngredientCode> SongIngredients =
    [
        IngredientCode.Facts, IngredientCode.FormChart, IngredientCode.ChordsOverLyrics, IngredientCode.Lyrics, IngredientCode.ChordChart,
        IngredientCode.Notation, IngredientCode.PracticeNotes, IngredientCode.PracticeProgress,
    ];

    // Columns follow SongIngredients: facts, form-chart, chords-over-lyrics, lyrics, chord-chart, notation, practice-notes, practice-progress.
    // "(in above)" of the table (lyrics inside chords over lyrics) is not offered separately.
    private static readonly Dictionary<HandoutFamily, IngredientOffer[]> Table = new()
    {
        [HandoutFamily.Overview] = [D, D, D, N, D, N, D, O],
        [HandoutFamily.Vocals] = [D, D, D, N, O, D, D, O],
        [HandoutFamily.Guitar] = [D, D, D, O, D, D, D, O],
        [HandoutFamily.Bass] = [D, D, O, O, D, D, D, O],
        [HandoutFamily.Keys] = [D, D, D, O, D, D, D, O],
        [HandoutFamily.Drums] = [D, D, N, O, N, D, D, O],
        [HandoutFamily.Melodic] = [D, D, O, O, D, D, D, O],
    };

    /// <summary>The family label (at most 60 characters), e.g. "Melodic instruments in B-flat".</summary>
    /// <param name="family">Family.</param>
    /// <param name="transposition">Transposition (melodic only).</param>
    /// <returns>The label.</returns>
    public static string Label(HandoutFamily family, Transposition? transposition = null) => family switch
    {
        HandoutFamily.Overview => "Band overview",
        HandoutFamily.Vocals => "Vocals",
        HandoutFamily.Guitar => "Guitar",
        HandoutFamily.Bass => "Bass",
        HandoutFamily.Keys => "Keys",
        HandoutFamily.Drums => "Drums",
        _ => transposition is null or Transposition.C ? "Melodic instruments" : $"Melodic instruments in {TranspositionName(transposition.Value)}",
    };

    /// <summary>The written name of a transposition (<c>B-flat</c>).</summary>
    /// <param name="transposition">Transposition.</param>
    /// <returns>The name.</returns>
    public static string TranspositionName(Transposition transposition) => transposition switch
    {
        Transposition.BFlat => "B-flat",
        Transposition.EFlat => "E-flat",
        Transposition.F => "F",
        _ => "C",
    };

    /// <summary>How a family offers an ingredient; <see cref="IngredientCode.SetlistNotes"/> is a default of every setlist handout only.</summary>
    /// <param name="family">Family.</param>
    /// <param name="code">Ingredient.</param>
    /// <param name="kind">Song or setlist.</param>
    /// <returns>The offer.</returns>
    public static IngredientOffer Offer(HandoutFamily family, IngredientCode code, HandoutKind kind) => code == IngredientCode.SetlistNotes
        ? kind == HandoutKind.Setlist ? D : N
        : Table[family][SongIngredients.ToList().IndexOf(code)];

    /// <summary>The ingredients a family offers, in print order.</summary>
    /// <param name="family">Family.</param>
    /// <param name="kind">Song or setlist.</param>
    /// <returns>The codes.</returns>
    public static IReadOnlyList<IngredientCode> Offered(HandoutFamily family, HandoutKind kind) =>
        SongIngredients.Append(IngredientCode.SetlistNotes).Where(code => Offer(family, code, kind) != N).ToList();

    /// <summary>The notation kinds that fit a family (data-model 6.1), best first.</summary>
    /// <param name="family">Family.</param>
    /// <param name="item">A notation item.</param>
    /// <param name="transposition">The melodic transposition.</param>
    /// <returns>True when the item fits.</returns>
    public static bool Fits(HandoutFamily family, NotationItem item, Transposition transposition)
    {
        ArgumentNullException.ThrowIfNull(item);
        return family switch
        {
            HandoutFamily.Vocals => item.Profile == ScoreProfile.Vocals,
            HandoutFamily.Guitar => item.Profile == ScoreProfile.Guitar,
            HandoutFamily.Bass => item.Profile == ScoreProfile.Bass,
            HandoutFamily.Keys => item.Profile is ScoreProfile.Piano or ScoreProfile.Keys,
            HandoutFamily.Drums => item.Kind == NotationKind.Drums,
            HandoutFamily.Melodic => FitsMelodic(item, transposition),
            _ => false,
        };
    }

    /// <summary>The notation a family includes by default: the newest fitting item per kind (score and tab for guitar and bass).</summary>
    /// <param name="family">Family.</param>
    /// <param name="items">The song's ready notation items, newest first.</param>
    /// <param name="transposition">The melodic transposition.</param>
    /// <returns>The default items.</returns>
    public static IReadOnlyList<NotationItem> DefaultNotation(HandoutFamily family, IReadOnlyList<NotationItem> items, Transposition transposition)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items.Where(item => Fits(family, item, transposition)).GroupBy(item => item.Kind).Select(group => group.First())
            .OrderBy(item => KindOrder(item.Kind)).ToList();
    }

    private static bool FitsMelodic(NotationItem item, Transposition transposition) => transposition == Transposition.C
        ? item.Kind == NotationKind.Score && (item.Profile is ScoreProfile.Strings or ScoreProfile.Brass or ScoreProfile.Woodwinds || item.Transposition == Transposition.C
            || item.Channel?.StartsWith("other", StringComparison.Ordinal) == true)
        : item.Kind == NotationKind.Transposed && item.Transposition == transposition;

    private static int KindOrder(NotationKind kind) => kind switch
    {
        NotationKind.Tab => 0,
        NotationKind.Transposed => 1,
        NotationKind.Drums => 2,
        _ => 3,
    };
}

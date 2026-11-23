using Microsoft.Extensions.Options;
using StemStudio.Api.Assets;
using StemStudio.Api.Handouts.Sources;
using StemStudio.Api.Insights;
using StemStudio.Api.Tracks;
using StemStudio.Domain.Handouts;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Handouts;

/// <summary>A validated handout request in domain terms (the recipe without the paper default applied).</summary>
/// <param name="Recipe">The recipe.</param>
/// <param name="Name">The requested item name, or null.</param>
public sealed record HandoutAsk(HandoutRecipe Recipe, string? Name);

/// <summary>A planned handout with its item facts.</summary>
/// <param name="Handout">The plan the assembler reads.</param>
/// <param name="Facts">The item facts (kind, family, songs, included, omitted).</param>
public sealed record HandoutDraft(PlannedHandout Handout, Domain.Handouts.HandoutFacts Facts);

/// <summary>
/// Resolves and plans a handout (FR-008..FR-010, data-model 6): the song (404, 409 <c>track-not-ready</c>) or the setlist (404,
/// 409 <c>setlist-empty</c>, <c>too-many-songs</c>; a song that is not completed is included with what it has), the material of
/// every song, the notation (requested ids must be ready score items of the songs, else 422) and the ingredients.
/// </summary>
/// <param name="tracks">Tracks.</param>
/// <param name="setlists">014 setlists.</param>
/// <param name="loader">Reads the songs' material.</param>
/// <param name="options">The setlist song limit.</param>
public sealed class HandoutPlanner(ITrackRepository tracks, ISetlistSource setlists, HandoutMaterialLoader loader, IOptions<InsightsOptions> options)
{
    /// <summary>Most notation items one song prints (data-model 2.5 <c>HandoutSong.notation</c>).</summary>
    public const int MaxNotationPerSong = 8;

    /// <summary>Detail of <c>track-not-ready</c> for a song handout.</summary>
    public const string TrackNotReady = "The track is still being processed; make a handout when it is completed.";

    /// <summary>Resolves the songs and plans the ingredients of a validated request.</summary>
    /// <param name="ask">The request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The draft, or the problem (404, 409, 422).</returns>
    public async Task<(HandoutDraft? Draft, IResult? Problem)> PlanAsync(HandoutAsk ask, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ask);
        var (songs, setlist, problem) = await SongsAsync(ask.Recipe, cancellationToken);
        if (problem is not null)
        {
            return (null, problem);
        }

        var recipe = ask.Recipe;
        var transposition = recipe.Transposition ?? Transposition.C;
        var materials = new List<HandoutMaterial>(songs!.Count);
        foreach (var (track, notes) in songs)
        {
            materials.Add(await loader.LoadAsync(track, recipe.Family, notes, cancellationToken));
        }

        return Plan(recipe, setlist, materials, transposition);
    }

    private static (HandoutDraft?, IResult?) Plan(HandoutRecipe recipe, SetlistSnapshot? setlist, List<HandoutMaterial> materials, Transposition transposition)
    {
        var known = materials.SelectMany(material => material.Notation).Select(item => item.AssetId).ToHashSet();
        if (recipe.NotationAssetIds?.Where(id => !known.Contains(id)).Cast<Guid?>().FirstOrDefault() is { } unknown)
        {
            return (null, HandoutProblems.Field("notationAssetIds", $"{unknown} is not a ready notation item of this {(setlist is null ? "song" : "setlist")}."));
        }

        var chosen = materials.Select(material => Chosen(material, recipe, transposition)).ToList();
        var notation = chosen.Select(items => (IReadOnlyList<NotationItem>)items.Take(MaxNotationPerSong).ToList()).ToList();
        var availability = materials.Select((material, index) => HandoutAvailability.Of(material, notation[index])).ToList();
        var combined = HandoutAvailability.Combine(availability, setlist?.Notes is not null);
        var plan = IngredientPlanner.Plan(recipe.Family, recipe.Kind, recipe.Include, combined);
        var songs = materials.Select((material, index) => new PlannedSong(material, notation[index], SongInclude(plan.Included, availability[index]))).ToList();
        var facts = new Domain.Handouts.HandoutFacts
        {
            Kind = recipe.Kind,
            Family = recipe.Family,
            Transposition = recipe.Family == HandoutFamily.Melodic ? transposition : null,
            Paper = recipe.Paper,
            Songs = songs.Count,
            Included = plan.Included,
            Omitted = plan.Omitted,
            Warnings = NotationWarnings(materials, chosen),
        };
        return (new HandoutDraft(new PlannedHandout(recipe.Kind, recipe.Family, transposition, setlist, songs), facts), null);
    }

    /// <summary>The notation of one song: the requested items of it, else the family defaults.</summary>
    /// <param name="material">The song.</param>
    /// <param name="recipe">The recipe.</param>
    /// <param name="transposition">Transposition.</param>
    /// <returns>At most eight items.</returns>
    public static IReadOnlyList<NotationItem> Notation(HandoutMaterial material, HandoutRecipe recipe, Transposition transposition)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(recipe);
        return Chosen(material, recipe, transposition).Take(MaxNotationPerSong).ToList();
    }

    private static IReadOnlyList<NotationItem> Chosen(HandoutMaterial material, HandoutRecipe recipe, Transposition transposition) =>
        recipe.NotationAssetIds is { } ids
            ? material.Notation.Where(item => ids.Contains(item.AssetId)).ToList()
            : Families.DefaultNotation(recipe.Family, material.Notation, transposition);

    /// <summary>A <c>notation-omitted</c> warning for every song with more chosen notation than it prints.</summary>
    private static List<HandoutWarning> NotationWarnings(List<HandoutMaterial> materials, List<IReadOnlyList<NotationItem>> chosen) =>
        materials.Select((material, index) => (material.Title, Dropped: chosen[index].Count - MaxNotationPerSong))
            .Where(song => song.Dropped > 0)
            .Select(song => new HandoutWarning(HandoutWarningCode.NotationOmitted, NotationOmittedText(song.Dropped, song.Title)))
            .Take(Domain.Assets.Asset.MaxHandoutWarnings)
            .ToList();

    /// <summary>The text of a song's dropped notation (at most 300 characters).</summary>
    /// <param name="dropped">How many items were left out.</param>
    /// <param name="title">Song title.</param>
    /// <returns>The text.</returns>
    public static string NotationOmittedText(int dropped, string title)
    {
        var items = dropped == 1 ? "1 sheet music item" : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{dropped} sheet music items");
        return $"{items} of \"{HandoutAssembler.Cut(title, 200)}\" {(dropped == 1 ? "was" : "were")} left out: a handout prints at most {MaxNotationPerSong} per song.";
    }

    private static List<IngredientCode> SongInclude(IReadOnlyList<IngredientCode> included, IReadOnlyDictionary<IngredientCode, IngredientAvailability> song) =>
        included.Where(code => code == IngredientCode.Facts || song.GetValueOrDefault(code) == IngredientAvailability.Available).ToList();

    private async Task<(List<(Track Track, string? Notes)>?, SetlistSnapshot?, IResult?)> SongsAsync(HandoutRecipe recipe, CancellationToken cancellationToken)
    {
        if (recipe.Kind == HandoutKind.Song)
        {
            var track = await tracks.FindAsync(recipe.TrackId!.Value, cancellationToken);
            return track switch
            {
                null => (null, null, TrackService.NotFound(recipe.TrackId.Value)),
                { Status: not TrackStatus.Completed } => (null, null, InsightsProblems.TrackNotReady(TrackNotReady)),
                _ => ([(track, null)], null, null),
            };
        }

        return await SetlistSongsAsync(recipe.SetlistId!.Value, cancellationToken);
    }

    private async Task<(List<(Track Track, string? Notes)>?, SetlistSnapshot?, IResult?)> SetlistSongsAsync(Guid setlistId, CancellationToken cancellationToken)
    {
        var setlist = await setlists.FindAsync(setlistId, cancellationToken);
        var max = options.Value.MaxSetlistHandoutSongs;
        var refusal = setlist switch
        {
            null => HandoutProblems.SetlistNotFound(setlistId),
            { Entries.Count: 0 } => InsightsProblems.SetlistEmpty(),
            _ when setlist.Entries.Count > max => InsightsProblems.TooManySongs(max),
            _ => null,
        };
        if (refusal is not null)
        {
            return (null, null, refusal);
        }

        var songs = new List<(Track, string?)>();
        var kept = new List<SetlistSong>();
        foreach (var entry in setlist!.Entries)
        {
            // A track deleted after the setlist was read (014 removes its entries) is left out; the positions close up.
            if (await tracks.FindAsync(entry.TrackId, cancellationToken) is { } track)
            {
                songs.Add((track, entry.Notes));
                kept.Add(entry with { Position = kept.Count + 1 });
            }
        }

        return songs.Count == 0 ? (null, null, InsightsProblems.SetlistEmpty()) : (songs, setlist with { Entries = kept }, null);
    }
}

/// <summary>Problems of the handout endpoints beside the conflicts of <see cref="InsightsProblems"/>.</summary>
public static class HandoutProblems
{
    /// <summary>422 with one field error.</summary>
    /// <param name="field">Field.</param>
    /// <param name="message">Message.</param>
    /// <returns>The problem.</returns>
    public static IResult Field(string field, string message) => ApiProblems.Unprocessable(new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] });

    /// <summary>422 with several field errors.</summary>
    /// <param name="errors">Field -> messages.</param>
    /// <returns>The problem.</returns>
    public static IResult Fields(IDictionary<string, string[]> errors) => ApiProblems.Unprocessable(errors);

    /// <summary>404 for an unknown setlist.</summary>
    /// <param name="setlistId">Setlist id.</param>
    /// <returns>The problem.</returns>
    public static IResult SetlistNotFound(Guid setlistId) => ApiProblems.NotFound($"Setlist {setlistId} does not exist.");

    /// <summary>409 <c>insufficient-disk-space</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult InsufficientDiskSpace() => AssetProblems.InsufficientDiskSpace();
}

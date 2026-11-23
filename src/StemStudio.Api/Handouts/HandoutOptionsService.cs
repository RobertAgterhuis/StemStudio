using System.Globalization;
using Microsoft.Extensions.Options;
using StemStudio.Api.Insights;
using StemStudio.Api.Handouts.Sources;
using StemStudio.Api.Tracks;
using StemStudio.Contracts.Handouts;
using StemStudio.Domain.Handouts;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Handouts;

/// <summary>
/// <c>GET /api/handouts/options</c> (FR-008, FR-010): the ingredients of a family with their status, default and the create
/// action of a missing one, the notation choices of the songs, the transpositions (melodic only) and an estimate.
/// </summary>
/// <param name="tracks">Tracks.</param>
/// <param name="setlists">014 setlists.</param>
/// <param name="loader">Reads the songs' material.</param>
/// <param name="options">The setlist song limit (<c>MaxSetlistHandoutSongs</c>, the same cap as creation).</param>
public sealed class HandoutOptionsService(ITrackRepository tracks, ISetlistSource setlists, HandoutMaterialLoader loader, IOptions<InsightsOptions> options)
{
    /// <summary>Most notation choices.</summary>
    public const int MaxChoices = 400;

    private static readonly Dictionary<string, HandoutFamily> FamilyNames = Enum.GetValues<HandoutFamily>().ToDictionary(value => IngredientPlanner.Wire(value), StringComparer.Ordinal);
    private static readonly Dictionary<string, Transposition> TranspositionNames = Enum.GetValues<Transposition>().ToDictionary(value => IngredientPlanner.Wire(value), StringComparer.Ordinal);

    /// <summary>The options of a song or setlist.</summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 404 or 422.</returns>
    public async Task<IResult> GetAsync(HandoutOptionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var (trackId, setlistId) = (query.TrackId, query.SetlistId);
        var (parsed, invalid) = Parse(query);
        if (invalid is not null)
        {
            return invalid;
        }

        var (songs, problem) = await SongsAsync(trackId, setlistId, parsed.Family, cancellationToken);
        return problem ?? TypedResults.Ok(Options(parsed.Family, parsed.Transposition, setlistId is null ? HandoutKind.Song : HandoutKind.Setlist, songs!));
    }

    private static ((HandoutFamily Family, Transposition Transposition), IResult?) Parse(HandoutOptionsQuery query)
    {
        var (family, transposition) = (query.Family, query.Transposition);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if ((query.TrackId is null) == (query.SetlistId is null))
        {
            errors["trackId"] = ["Give exactly one of trackId and setlistId."];
        }

        if (!FamilyNames.TryGetValue(family ?? string.Empty, out var parsedFamily))
        {
            errors["family"] = ["Must be one of overview, vocals, guitar, bass, keys, drums, melodic."];
        }

        var parsedTransposition = Transposition.C;
        if (transposition is not null && (!TranspositionNames.TryGetValue(transposition, out parsedTransposition) || parsedFamily != HandoutFamily.Melodic))
        {
            errors["transposition"] = ["A transposition (c, b-flat, e-flat, f) is only offered for the melodic family."];
        }

        return ((parsedFamily, parsedTransposition), errors.Count == 0 ? null : HandoutProblems.Fields(errors));
    }

    private async Task<(List<(HandoutMaterial Material, Track Track)>?, IResult?)> SongsAsync(Guid? trackId, Guid? setlistId, HandoutFamily family, CancellationToken cancellationToken)
    {
        if (trackId is { } id)
        {
            return await tracks.FindAsync(id, cancellationToken) is { } track
                ? ([(await loader.LoadAsync(track, family, null, cancellationToken), track)], null)
                : (null, TrackService.NotFound(id));
        }

        if (await setlists.FindAsync(setlistId!.Value, cancellationToken) is not { } setlist)
        {
            return (null, HandoutProblems.SetlistNotFound(setlistId.Value));
        }

        // At most the song limit is read: a longer setlist cannot be made (409 too-many-songs on create), so its options are
        // those of the first songs and the request stays bounded.
        var songs = new List<(HandoutMaterial, Track)>();
        foreach (var entry in setlist.Entries.Take(options.Value.MaxSetlistHandoutSongs))
        {
            if (await tracks.FindAsync(entry.TrackId, cancellationToken) is { } track)
            {
                songs.Add((await loader.LoadAsync(track, family, entry.Notes, cancellationToken), track));
            }
        }

        return (songs, null);
    }

    private static HandoutOptionsDto Options(HandoutFamily family, Transposition transposition, HandoutKind kind, List<(HandoutMaterial Material, Track Track)> songs)
    {
        var recipe = new HandoutRecipe { Kind = kind, Family = family, Paper = Paper.A4, Transposition = transposition };
        var notation = songs.Select(song => HandoutPlanner.Notation(song.Material, recipe, transposition)).ToList();
        var availability = HandoutAvailability.Combine(songs.Select((song, index) => HandoutAvailability.Of(song.Material, notation[index])).ToList(), false);
        var single = kind == HandoutKind.Song && songs.Count == 1 ? songs[0] : ((HandoutMaterial Material, Track Track)?)null;
        return new HandoutOptionsDto
        {
            Kind = HandoutMapper.Convert<HandoutKindDto>(kind),
            Family = HandoutMapper.Convert<HandoutFamilyDto>(family),
            FamilyLabel = Families.Label(family, transposition),
            Songs = songs.Count,
            Ingredients = Families.Offered(family, kind)
                .Select(code => IngredientTexts.Ingredient(code, availability.GetValueOrDefault(code, IngredientAvailability.Missing), new OptionsContext(family, kind, single, notation.FirstOrDefault() ?? [])))
                .ToList(),
            Notation = Choices(family, transposition, songs, notation),
            Transpositions = family == HandoutFamily.Melodic ? [TranspositionDto.C, TranspositionDto.BFlat, TranspositionDto.EFlat, TranspositionDto.F] : [],
            DefaultPaper = PaperDto.A4,
            EstimateSeconds = 4 + (2 * Math.Max(songs.Count, 1)),
        };
    }

    private static List<NotationChoiceDto> Choices(HandoutFamily family, Transposition transposition, List<(HandoutMaterial Material, Track Track)> songs, List<IReadOnlyList<NotationItem>> defaults) =>
        songs.SelectMany((song, index) => song.Material.Notation.Where(item => Families.Fits(family, item, transposition)).Select(item => new NotationChoiceDto
        {
            AssetId = item.AssetId,
            Title = HandoutAssembler.Cut(item.Title, 120)!,
            Kind = HandoutMapper.Convert<NotationKindDto>(item.Kind),
            Transposition = item.Kind == NotationKind.Transposed && item.Transposition is { } written ? HandoutMapper.Convert<TranspositionDto>(written) : null,
            DefaultSelected = defaults[index].Contains(item),
            SongTitle = HandoutAssembler.Cut(song.Material.Title, 300)!,
        })).Take(MaxChoices).ToList();
}

/// <summary>Texts and create actions of the ingredient list (backend 1.19.0 <c>Ingredient</c>).</summary>
internal static class IngredientTexts
{
    public static IngredientDto Ingredient(IngredientCode code, IngredientAvailability availability, OptionsContext context)
    {
        var (family, kind, song, notation) = (context.Family, context.Kind, context.Song, context.Notation);
        var available = availability == IngredientAvailability.Available;
        var material = song?.Material;
        return new IngredientDto
        {
            Code = HandoutMapper.Convert<IngredientCodeDto>(code),
            Status = available ? IngredientStatusDto.Available : IngredientStatusDto.Missing,
            DefaultIncluded = Families.Offer(family, code, kind) == IngredientOffer.Default,
            Text = HandoutAssembler.Cut(Text(code, availability, material, notation), 200)!,
            Action = material is null ? null : Action(code, available, context),
        };
    }

    private static string Text(IngredientCode code, IngredientAvailability availability, HandoutMaterial? material, IReadOnlyList<NotationItem> notation) => (code, availability) switch
    {
        (IngredientCode.Facts, _) => material is null ? "Tempo, metre, key and length of every song" : Facts(material),
        (_, IngredientAvailability.NoTiming) => "No timing yet (beat grid or synced lyrics missing)",
        (IngredientCode.FormChart, IngredientAvailability.Available) => material is null ? "Sections of the songs" : Count(material.Sections.Count, "section"),
        (IngredientCode.FormChart, _) => "No sections yet",
        (IngredientCode.ChordsOverLyrics, IngredientAvailability.Available) => "Chords above the lyrics",
        (IngredientCode.ChordsOverLyrics, _) => "Needs chords and lyrics",
        (IngredientCode.Lyrics, IngredientAvailability.Available) => "Lyrics",
        (IngredientCode.Lyrics, _) => "No lyrics yet",
        (IngredientCode.ChordChart, IngredientAvailability.Available) => material?.Chart is { } chart ? Count(HandoutChords.Legend(chart, Transposition.C).Count, "chord") : "Chords per bar",
        (IngredientCode.ChordChart, _) => "No chords yet",
        (IngredientCode.Notation, IngredientAvailability.Available) => material is null ? "Notation of the songs" : string.Join("; ", notation.Select(item => item.Title)),
        (IngredientCode.Notation, _) => "Notation not generated yet",
        (IngredientCode.PracticeNotes, IngredientAvailability.Available) => "Your practice notes",
        (IngredientCode.PracticeNotes, _) => "No notes yet",
        (IngredientCode.PracticeProgress, IngredientAvailability.Available) => "Tempo reached per section",
        (IngredientCode.PracticeProgress, _) => "No practice log yet",
        (IngredientCode.SetlistNotes, IngredientAvailability.Available) => "Setlist and song notes",
        _ => "No setlist notes",
    };

    private static IngredientActionDto? Action(IngredientCode code, bool available, OptionsContext context) => code switch
    {
        IngredientCode.FormChart when !available => IngredientActionDto.DetectSections,
        IngredientCode.ChordsOverLyrics or IngredientCode.ChordChart when !available && !context.Song!.Value.Material.HasChords => IngredientActionDto.DetectChords,
        IngredientCode.ChordsOverLyrics or IngredientCode.Lyrics when !available && !context.Song!.Value.Material.HasLyrics => IngredientActionDto.FindLyrics,
        IngredientCode.Notation => NotationAction(context.Family, available, context.Song!.Value.Track, context.Notation),
        IngredientCode.PracticeNotes when !available => IngredientActionDto.WriteNotes,
        _ => null,
    };

    private static IngredientActionDto? NotationAction(HandoutFamily family, bool available, Track track, IReadOnlyList<NotationItem> notation) => family switch
    {
        HandoutFamily.Guitar or HandoutFamily.Bass when notation.All(item => item.Kind != NotationKind.Tab) => IngredientActionDto.CreateTab,
        HandoutFamily.Drums when !available && !DrumsSplit(track) => IngredientActionDto.SplitDrums,
        HandoutFamily.Drums when !available => IngredientActionDto.CreateDrumNotation,
        HandoutFamily.Melodic when !available && notation.Count == 0 => IngredientActionDto.CreateTransposedPart,
        _ when !available => IngredientActionDto.CreateScore,
        _ => null,
    };

    private static bool DrumsSplit(Track track) => track.DetailPhase is { Status: DetailPhaseStatus.Completed, FilesRemovedAt: null } phase
        && phase.Parents.Any(parent => parent.Parent == "drums" && parent.Outcome == ParentOutcomeKind.Split);

    private static string Facts(HandoutMaterial material)
    {
        var parts = new List<string>();
        if (material.Grid is { Bpm: > 0 } grid)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"Tempo {Math.Round(grid.Bpm):0} BPM"));
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{grid.BeatsPerBar}/4"));
        }

        if (HandoutChords.Key(material.Chart, Transposition.C) is { } key)
        {
            parts.Add(key);
        }

        return parts.Count == 0 ? string.Create(CultureInfo.InvariantCulture, $"Length {TimeSpan.FromSeconds(material.DurationSeconds):m\\:ss}") : string.Join(", ", parts);
    }

    private static string Count(int count, string noun) => string.Create(CultureInfo.InvariantCulture, $"{count} {noun}{(count == 1 ? string.Empty : "s")}");
}

/// <summary>The query of <c>getHandoutOptions</c> (enum values are parsed by their wire names).</summary>
/// <param name="TrackId">The song, or null.</param>
/// <param name="SetlistId">The setlist, or null.</param>
/// <param name="Family">Family wire value.</param>
/// <param name="Transposition">Transposition wire value, or null.</param>
public sealed record HandoutOptionsQuery(Guid? TrackId, Guid? SetlistId, string? Family, string? Transposition);

/// <summary>What the ingredient texts and actions of one options answer depend on.</summary>
/// <param name="Family">Family.</param>
/// <param name="Kind">Song or setlist.</param>
/// <param name="Song">The song of a song handout; null for a setlist.</param>
/// <param name="Notation">The default notation of the (first) song.</param>
internal sealed record OptionsContext(HandoutFamily Family, HandoutKind Kind, (HandoutMaterial Material, Track Track)? Song, IReadOnlyList<NotationItem> Notation);

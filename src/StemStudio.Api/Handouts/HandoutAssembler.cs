#pragma warning disable IDE0005, CS9113, CS1998, IDE0060, CA1822, CA1801, IDE0051, IDE0052, CS0169, CS0414
using System.Globalization;
using System.Reflection;
using StemStudio.Api.Worker;
using StemStudio.Domain.Handouts;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Scores;

namespace StemStudio.Api.Handouts;

/// <summary>One song of a planned handout: its material, its notation and what it prints.</summary>
/// <param name="Material">The song's material.</param>
/// <param name="Notation">The notation chosen for the family.</param>
/// <param name="Include">The ingredients this song prints.</param>
public sealed record PlannedSong(HandoutMaterial Material, IReadOnlyList<NotationItem> Notation, IReadOnlyList<IngredientCode> Include);

/// <summary>The handout as planned: kind, family, transposition, the setlist (or null) and the songs.</summary>
/// <param name="Kind">Song or setlist.</param>
/// <param name="Family">Family.</param>
/// <param name="Transposition">Transposition (melodic; else <see cref="Transposition.C"/>).</param>
/// <param name="Setlist">The setlist, or null for a song handout.</param>
/// <param name="Songs">The songs in order.</param>
public sealed record PlannedHandout(HandoutKind Kind, HandoutFamily Family, Transposition Transposition, SetlistSnapshot? Setlist, IReadOnlyList<PlannedSong> Songs);

/// <summary>
/// Assembles the complete, resolved handout document the worker lays out (data-model 2.5, 6.3): facts, form chart with bars
/// and chords, lyrics with chords (016 chord sheet), notation, notes, fixed notice texts, attributions and the date from the
/// clock. The worker performs no lookups.
/// </summary>
/// <param name="clock">Clock (<c>generatedOn</c>).</param>
public sealed class HandoutAssembler(TimeProvider clock)
{
    /// <summary>Longest app version on the last page.</summary>
    public const int MaxAppVersionLength = 40;

    private static readonly string AppVersion = ReadAppVersion();

    /// <summary>The document of a planned handout.</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>The document.</returns>
    public WorkerHandoutDocument Document(PlannedHandout plan)
    {
        throw new NotImplementedException();
    }

    /// <summary>One song of the document.</summary>
    /// <param name="song">The planned song.</param>
    /// <param name="transposition">Transposition.</param>
    /// <returns>The song.</returns>
    public static WorkerHandoutSong Song(PlannedSong song, Transposition transposition)
    {
        throw new NotImplementedException();
    }

    private static WorkerHandoutLyrics? Lyrics(HandoutMaterial material, HashSet<IngredientCode> include, Transposition transposition)
    {
        if (material.Lyrics is not { } document)
        {
            return null;
        }

        var lines = include.Contains(IngredientCode.ChordsOverLyrics) && material.Chart is { } chart
            ? HandoutChords.OverLyrics(chart, (document, material.LyricsOffsetMs), material.Grid, transposition)
            : include.Contains(IngredientCode.Lyrics) ? HandoutChords.Plain(document) : null;
        return lines is null
            ? null
            : new WorkerHandoutLyrics(
                lines.Lines.Select(line => new WorkerLyricLine(line.Text, line.Section, line.Chords.Select(chord => new WorkerLyricChord(chord.Symbol, chord.CharIndex)).ToList())).ToList(),
                lines.Placement.ToString().ToLowerInvariant(),
                Cut(LyricsCredit.For(document), 200) ?? "Lyrics");
    }

    private static WorkerSetlistBlock SetlistBlock(SetlistSnapshot setlist, PlannedHandout plan) => new(
        Cut(setlist.Name, 200)!,
        Cut(setlist.Notes, 4000),
        plan.Songs.Select((song, index) => new WorkerSetlistEntryBlock(
            setlist.Entries[index].Position,
            Cut(song.Material.Title, 300)!,
            Cut(song.Material.Artist, 500),
            HandoutChords.Key(song.Material.Chart, Transposition.C),
            SongFactsBuilder.Tempo(song.Material),
            song.Material.DurationSeconds,
            Cut(song.Material.EntryNotes, 4000))).ToList());

    private static AttributionSources Sources(PlannedSong song)
    {
        var include = song.Include.ToHashSet();
        var lyrics = include.Contains(IngredientCode.ChordsOverLyrics) || include.Contains(IngredientCode.Lyrics) ? song.Material.Lyrics : null;
        var notation = include.Contains(IngredientCode.Notation) ? song.Notation : [];
        return new AttributionSources(lyrics, UsesChords(include) && song.Material.HasChords, notation, song.Material.ArtworkPath is null ? null : song.Material.Artwork);
    }

    private static bool UsesChords(HashSet<IngredientCode> include) => include.Contains(IngredientCode.ChordChart) || include.Contains(IngredientCode.ChordsOverLyrics);

    private static string ReadAppVersion()
    {
        var version = typeof(HandoutAssembler).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        var plain = version.Split('+')[0];
        return plain.Length > MaxAppVersionLength ? plain[..MaxAppVersionLength] : plain;
    }

    /// <summary>Trims and cuts a text; null stays null.</summary>
    /// <param name="text">Text.</param>
    /// <param name="max">Maximum length.</param>
    /// <returns>The text or null.</returns>
    internal static string? Cut(string? text, int max) => text is null ? null : text.Length <= max ? text : text[..max];
}

/// <summary>The facts row of a handout song (data-model 2.5 <c>SongFacts</c>).</summary>
internal static class SongFactsBuilder
{
    public static WorkerSongFacts Of(PlannedSong song, Transposition transposition)
    {
        throw new NotImplementedException();
    }

    public static int? Tempo(HandoutMaterial material) => throw new NotImplementedException();

    private static string ScoreKeyText(KeyInfo key) =>
        $"{key.Tonic.Replace("-", "b", StringComparison.Ordinal)} {(key.Mode == KeyMode.Major ? "major" : "minor")}";
}

/// <summary>The form chart of a handout song (data-model 2.5 <c>HandoutSection</c>).</summary>
internal static class HandoutSections
{
    public const int MaxSections = 64;

    public static IReadOnlyList<WorkerHandoutSection> Of(HandoutMaterial material, HashSet<IngredientCode> include, Transposition transposition)
    {
        throw new NotImplementedException();
    }
}

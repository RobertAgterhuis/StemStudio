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
        ArgumentNullException.ThrowIfNull(plan);
        var first = plan.Songs[0].Material;
        return new WorkerHandoutDocument
        {
            Kind = plan.Kind,
            Family = plan.Family,
            FamilyLabel = Families.Label(plan.Family, plan.Transposition),
            Title = Cut(plan.Setlist?.Name ?? first.Title, 300)!,
            Subtitle = plan.Setlist is null ? Cut(first.Artist, 300) : string.Create(CultureInfo.InvariantCulture, $"Setlist - {plan.Songs.Count} songs"),
            GeneratedOn = clock.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Notice = WorkerHandoutNotice.Fixed,
            Setlist = plan.Setlist is { } setlist ? SetlistBlock(setlist, plan) : null,
            Songs = plan.Songs.Select(song => Song(song, plan.Transposition)).ToList(),
            Attributions = HandoutNotice.Attributions(plan.Songs.Select(Sources)),
            AppVersion = AppVersion,
        };
    }

    /// <summary>One song of the document.</summary>
    /// <param name="song">The planned song.</param>
    /// <param name="transposition">Transposition.</param>
    /// <returns>The song.</returns>
    public static WorkerHandoutSong Song(PlannedSong song, Transposition transposition)
    {
        ArgumentNullException.ThrowIfNull(song);
        var material = song.Material;
        var include = song.Include.ToHashSet();
        return new WorkerHandoutSong
        {
            Title = Cut(material.Title, 300)!,
            Artist = Cut(material.Artist, 500),
            ArtworkPath = material.ArtworkPath,
            Facts = SongFactsBuilder.Of(song, transposition),
            Sections = HandoutSections.Of(material, include, transposition),
            Lyrics = Lyrics(material, include, transposition),
            ChordSymbols = UsesChords(include) && material.Chart is { } chart ? HandoutChords.Legend(chart, transposition) : [],
            Notation = include.Contains(IngredientCode.Notation)
                ? song.Notation.Take(8).Select(item => new WorkerHandoutNotation(Cut(item.Title, 120)!, item.Kind, item.PdfPath)).ToList()
                : [],
            PracticeNotes = include.Contains(IngredientCode.PracticeNotes) ? material.PracticeNote : null,
            EntryNotes = include.Contains(IngredientCode.SetlistNotes) ? material.EntryNotes : null,
            Include = song.Include,
        };
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
        var material = song.Material;
        var chordKey = HandoutChords.Key(material.Chart, transposition);
        var scoreKey = chordKey is null ? material.Notation.Concat(song.Notation).Select(item => item.Key).OfType<KeyInfo>().FirstOrDefault() : null;
        return new WorkerSongFacts(
            Tempo(material),
            material.Grid is { BeatsPerBar: >= 1 and <= 99 } grid ? string.Create(CultureInfo.InvariantCulture, $"{grid.BeatsPerBar}/4") : null,
            chordKey ?? (scoreKey is null ? null : ScoreKeyText(scoreKey)),
            chordKey is not null ? "chords" : scoreKey is not null ? "score" : null,
            Math.Max(0, material.DurationSeconds),
            HandoutChords.TranspositionNote(transposition));
    }

    public static int? Tempo(HandoutMaterial material) =>
        material.Grid is { Bpm: > 0 } grid ? Math.Clamp((int)Math.Round(grid.Bpm, MidpointRounding.AwayFromZero), 20, 400) : null;

    private static string ScoreKeyText(KeyInfo key) =>
        $"{key.Tonic.Replace("-", "b", StringComparison.Ordinal)} {(key.Mode == KeyMode.Major ? "major" : "minor")}";
}

/// <summary>The form chart of a handout song (data-model 2.5 <c>HandoutSection</c>).</summary>
internal static class HandoutSections
{
    public const int MaxSections = 64;

    public static IReadOnlyList<WorkerHandoutSection> Of(HandoutMaterial material, HashSet<IngredientCode> include, Transposition transposition)
    {
        if (!include.Contains(IngredientCode.FormChart) && !include.Contains(IngredientCode.ChordChart) && !include.Contains(IngredientCode.PracticeProgress))
        {
            return [];
        }

        var chords = ChordsOf(material, include, transposition);
        return material.Sections.Take(MaxSections).Select(section =>
        {
            var bars = BarMapper.Map(section, material.Grid, chords);
            var name = HandoutAssembler.Cut(string.IsNullOrWhiteSpace(section.Label) ? "Section" : section.Label, 80)!;
            return new WorkerHandoutSection(name, Math.Max(0, section.StartSeconds), Math.Max(0, section.EndSeconds), bars.StartBar, bars.Bars, bars.ChordBars, Tempo(material, include, section));
        }).ToList();
    }

    private static (Domain.InstrumentViews.ChordChart, Transposition)? ChordsOf(HandoutMaterial material, HashSet<IngredientCode> include, Transposition transposition) =>
        include.Contains(IngredientCode.ChordChart) && material.Chart is { } chart ? (chart, transposition) : null;

    private static int? Tempo(HandoutMaterial material, HashSet<IngredientCode> include, SongSection section) =>
        include.Contains(IngredientCode.PracticeProgress) && material.BestTempo.TryGetValue(section.Id, out var percent) ? Math.Clamp(percent, 25, 200) : null;
}

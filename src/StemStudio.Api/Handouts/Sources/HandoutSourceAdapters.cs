using StemStudio.Contracts;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Handouts;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Practice;
using StemStudio.Domain.Sections;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Handouts.Sources;

/// <summary>Sections from 014's <see cref="ISectionRepository"/>; the start bar as 014 shows it (<see cref="SectionSnapper.BarAt"/>).</summary>
/// <param name="sections">Section repository.</param>
public sealed class SectionSourceAdapter(ISectionRepository sections) : ISectionSource
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SongSection>> SectionsAsync(Track track, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        var set = await sections.FindAsync(track.Id, cancellationToken);
        var grid = track.BeatGrid is { Reliable: true } reliable ? reliable : null;
        return (set?.Sections ?? []).OrderBy(section => section.Position)
            .Select(section => new SongSection(section.Id, section.Label, section.StartSeconds, section.EndSeconds, SectionSnapper.BarAt(section.StartSeconds, grid)))
            .ToList();
    }
}

/// <summary>Setlists from 014's <see cref="ISetlistRepository"/>: positions 1-based, empty notes (014 stores <c>""</c>) become null.</summary>
/// <param name="setlists">Setlist repository.</param>
public sealed class SetlistSourceAdapter(ISetlistRepository setlists) : ISetlistSource
{
    /// <inheritdoc />
    public async Task<SetlistSnapshot?> FindAsync(Guid setlistId, CancellationToken cancellationToken)
    {
        if (await setlists.FindAsync(setlistId, cancellationToken) is not { } setlist)
        {
            return null;
        }

        var entries = setlist.Entries.OrderBy(entry => entry.Position).Select(entry => new SetlistSong(entry.Position + 1, entry.TrackId, NoteOrNull(entry.Note))).ToList();
        return new SetlistSnapshot(setlist.Id, setlist.Name, NoteOrNull(setlist.Note), entries);
    }

    private static string? NoteOrNull(string? note) => string.IsNullOrWhiteSpace(note) ? null : note;
}

/// <summary>Best trainer tempo per section from 014's practice log (<see cref="PracticeSummaryCalculator"/> over all sessions).</summary>
/// <param name="practice">Practice repository.</param>
/// <param name="clock">Clock.</param>
public sealed class PracticeLogSourceAdapter(IPracticeRepository practice, TimeProvider clock) : IPracticeLogSource
{
    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> BestTempoAsync(Guid trackId, CancellationToken cancellationToken)
    {
        var sessions = await practice.SessionsOfTrackAsync(trackId, cancellationToken);
        var window = new SummaryWindow(clock.GetUtcNow().UtcDateTime, ContractLimits.MaxSummaryDays, 0);
        return PracticeSummaryCalculator.Summarize(sessions, window, new Dictionary<Guid, string>()).Sections
            .Where(total => total is { SectionId: not null, BestTrainerPercent: not null })
            .ToDictionary(total => total.SectionId!.Value, total => (int)Math.Round(total.BestTrainerPercent!.Value, MidpointRounding.AwayFromZero));
    }
}

/// <summary>The chord chart from 016's <see cref="ITrackAnalysisRepository"/> (ready or stale, as 016 shows it).</summary>
/// <param name="analyses">Analysis repository.</param>
public sealed class ChordSourceAdapter(ITrackAnalysisRepository analyses) : IChordSource
{
    /// <inheritdoc />
    public async Task<ChordChart?> ChartAsync(Guid trackId, CancellationToken cancellationToken) =>
        await analyses.FindAsync(trackId, AnalysisKind.Chords, cancellationToken) is { Status: AnalysisStatus.Ready or AnalysisStatus.Stale, Document: ChordChart chart }
            ? chart
            : null;
}

/// <summary>Ready score items of a song with a PDF (008/016), classified by <see cref="NotationClassifier"/>.</summary>
/// <param name="assets">Asset repository.</param>
public sealed class NotationCatalogAdapter(IAssetRepository assets) : INotationCatalog
{
    /// <summary>Most items read per song.</summary>
    public const int MaxItems = 400;

    /// <inheritdoc />
    public async Task<IReadOnlyList<NotationItem>> ItemsAsync(Guid trackId, CancellationToken cancellationToken)
    {
        var page = await assets.ListAsync(new AssetQuery { Kind = AssetKind.Score, TrackId = trackId, Status = AssetStatus.Ready, Limit = MaxItems }, cancellationToken);
        return page.Items.Select(ToItem).OfType<NotationItem>().OrderByDescending(item => item.CreatedAt).ToList();
    }

    private static NotationItem? ToItem(Asset asset)
    {
        if (asset is not { Score: { } facts, TrackId: { } trackId } || asset.Files.FirstOrDefault(file => file.Role == "pdf") is not { } pdf)
        {
            return null;
        }

        var (kind, transposition) = NotationClassifier.Classify(facts);
        return new NotationItem
        {
            AssetId = asset.Id,
            Title = asset.Name,
            TrackId = trackId,
            Profile = facts.Profile,
            Kind = kind,
            Transposition = transposition,
            Channel = asset.Provenance.ScoreRecipe?.Source.Channel,
            PdfPath = $"assets/{asset.Id}/{pdf.FileName}",
            SizeBytes = pdf.SizeBytes,
            Key = facts.Key,
            Attribution = facts.Attribution,
            CreatedAt = asset.CreatedAt,
        };
    }
}

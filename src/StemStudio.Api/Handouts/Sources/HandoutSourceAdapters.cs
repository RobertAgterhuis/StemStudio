#pragma warning disable IDE0005, CS9113, CS1998, IDE0060, CA1822, CA1801, IDE0051, IDE0052, CS0169, CS0414
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
    public Task<IReadOnlyList<SongSection>> SectionsAsync(Track track, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

/// <summary>Setlists from 014's <see cref="ISetlistRepository"/>: positions 1-based, empty notes (014 stores <c>""</c>) become null.</summary>
/// <param name="setlists">Setlist repository.</param>
public sealed class SetlistSourceAdapter(ISetlistRepository setlists) : ISetlistSource
{
    /// <inheritdoc />
    public Task<SetlistSnapshot?> FindAsync(Guid setlistId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private static string? NoteOrNull(string? note) => string.IsNullOrWhiteSpace(note) ? null : note;
}

/// <summary>Best trainer tempo per section from 014's practice log (<see cref="PracticeSummaryCalculator"/> over all sessions).</summary>
/// <param name="practice">Practice repository.</param>
/// <param name="clock">Clock.</param>
public sealed class PracticeLogSourceAdapter(IPracticeRepository practice, TimeProvider clock) : IPracticeLogSource
{
    /// <inheritdoc />
    public Task<IReadOnlyDictionary<Guid, int>> BestTempoAsync(Guid trackId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

/// <summary>The chord chart from 016's <see cref="ITrackAnalysisRepository"/> (ready or stale, as 016 shows it).</summary>
/// <param name="analyses">Analysis repository.</param>
public sealed class ChordSourceAdapter(ITrackAnalysisRepository analyses) : IChordSource
{
    /// <inheritdoc />
    public Task<ChordChart?> ChartAsync(Guid trackId, CancellationToken cancellationToken) => throw new NotImplementedException();
}

/// <summary>Ready score items of a song with a PDF (008/016), classified by <see cref="NotationClassifier"/>.</summary>
/// <param name="assets">Asset repository.</param>
public sealed class NotationCatalogAdapter(IAssetRepository assets) : INotationCatalog
{
    /// <summary>Most items read per song.</summary>
    public const int MaxItems = 400;

    /// <inheritdoc />
    public Task<IReadOnlyList<NotationItem>> ItemsAsync(Guid trackId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
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
            Key = facts.Key,
            Attribution = facts.Attribution,
            CreatedAt = asset.CreatedAt,
        };
    }
}

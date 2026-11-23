using StemStudio.Domain.Handouts;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Handouts.Sources;

/// <summary>Sections of a song (014; data-model 8 A-014-1). One adapter per source, so a shape change touches one class.</summary>
public interface ISectionSource
{
    /// <summary>The song's sections in order; empty when it has none.</summary>
    /// <param name="track">The song.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The sections.</returns>
    Task<IReadOnlyList<SongSection>> SectionsAsync(Track track, CancellationToken cancellationToken);
}

/// <summary>Setlists (014; A-014-2).</summary>
public interface ISetlistSource
{
    /// <summary>A setlist with its entries in running order.</summary>
    /// <param name="setlistId">Setlist id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The setlist, or null when unknown.</returns>
    Task<SetlistSnapshot?> FindAsync(Guid setlistId, CancellationToken cancellationToken);
}

/// <summary>The practice log (014; A-014-3).</summary>
public interface IPracticeLogSource
{
    /// <summary>Best trainer tempo reached per section (rounded percent; sections without one are absent).</summary>
    /// <param name="trackId">The song.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Section id -> percent.</returns>
    Task<IReadOnlyDictionary<Guid, int>> BestTempoAsync(Guid trackId, CancellationToken cancellationToken);
}

/// <summary>The detected chords of a song (016 chord chart; A-016-1).</summary>
public interface IChordSource
{
    /// <summary>The ready (or stale) chord chart of a song.</summary>
    /// <param name="trackId">The song.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The chart, or null when the song has no chords.</returns>
    Task<ChordChart?> ChartAsync(Guid trackId, CancellationToken cancellationToken);
}

/// <summary>The ready notation items of a song (008/016 score items; A-016-2).</summary>
public interface INotationCatalog
{
    /// <summary>The ready score items of a song that have a PDF, newest first.</summary>
    /// <param name="trackId">The song.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The items.</returns>
    Task<IReadOnlyList<NotationItem>> ItemsAsync(Guid trackId, CancellationToken cancellationToken);
}

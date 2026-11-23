using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.InstrumentViews;

namespace StemStudio.Persistence;

/// <summary>Instrument views (feature 016): the per-track analyses.</summary>
public sealed partial class StemStudioDbContext
{
    /// <summary>Chords, drum hits and reference melody per track (feature 016).</summary>
    public DbSet<TrackAnalysis> TrackAnalyses => Set<TrackAnalysis>();
}

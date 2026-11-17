using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Practice;
using StemStudio.Domain.Sections;
using StemStudio.Domain.Setlists;

namespace StemStudio.Persistence;

/// <summary>Sets of feature 014 (practice flow): sections, score views, setlists and the practice log.</summary>
public sealed partial class StemStudioDbContext
{
    /// <summary>Section sets, one per track (feature 014).</summary>
    public DbSet<TrackSections> TrackSections => Set<TrackSections>();

    /// <summary>Sections of the section sets (feature 014).</summary>
    public DbSet<Section> Sections => Set<Section>();

    /// <summary>Score views, one per score asset (feature 014).</summary>
    public DbSet<ScoreView> ScoreViews => Set<ScoreView>();

    /// <summary>Setlists (feature 014).</summary>
    public DbSet<Setlist> Setlists => Set<Setlist>();

    /// <summary>Setlist entries (feature 014).</summary>
    public DbSet<SetlistEntry> SetlistEntries => Set<SetlistEntry>();

    /// <summary>The practice log switch (single row, feature 014).</summary>
    public DbSet<PracticeLogSettings> PracticeLogSettings => Set<PracticeLogSettings>();

    /// <summary>Practice sessions (feature 014).</summary>
    public DbSet<PracticeSession> PracticeSessions => Set<PracticeSession>();
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Practice;
using StemStudio.Domain.Sections;
using StemStudio.Domain.Setlists;
using StemStudio.Domain.Tracks;

namespace StemStudio.Persistence.Configurations;

/// <summary>Maps <see cref="TrackSections"/> to <c>TrackSections</c> (one row per track, deleted with the track).</summary>
public sealed class TrackSectionsConfiguration : IEntityTypeConfiguration<TrackSections>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TrackSections> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("TrackSections");
        builder.HasKey(set => set.TrackId);
        builder.Property(set => set.TrackId).ValueGeneratedNever();
        builder.HasOne<Track>().WithOne().HasForeignKey<TrackSections>(set => set.TrackId).OnDelete(DeleteBehavior.Cascade);
        ConfigureColumns(builder);
        builder.HasMany(set => set.Sections).WithOne().HasForeignKey(section => section.TrackId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(set => set.Sections).HasField("_sections").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(set => set.Status);
    }

    private static void ConfigureColumns(EntityTypeBuilder<TrackSections> builder)
    {
        builder.Property(set => set.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(set => set.Origin).HasConversion<string>().HasMaxLength(16);
        builder.Property(set => set.Snap).HasConversion<string>().HasMaxLength(16);
        builder.Property(set => set.Analyzer).HasMaxLength(64);
        builder.Property(set => set.WorkerJobId).HasMaxLength(32);
        builder.Property(set => set.Error).HasMaxLength(TrackSections.MaxErrorLength);
        builder.Property(set => set.Warnings).HasColumnName("WarningsJson")
            .HasConversion(JsonColumn<IReadOnlyList<SectionWarning>>.Converter, JsonColumn<IReadOnlyList<SectionWarning>>.Comparer);
    }
}

/// <summary>Maps <see cref="Section"/> to <c>Sections</c> (ordered by position per track; the domain keeps positions unique, a unique index would break reordering updates).</summary>
public sealed class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Section> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Sections");
        builder.HasKey(section => section.Id);
        builder.Property(section => section.Id).ValueGeneratedNever();
        builder.HasIndex(section => new { section.TrackId, section.Position });
        builder.Property(section => section.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(section => section.Origin).HasConversion<string>().HasMaxLength(16);
        builder.Property(section => section.Label).HasMaxLength(SectionRules.MaxLabelLength);
        builder.Property(section => section.Instrument).HasMaxLength(64);
    }
}

/// <summary>Maps <see cref="ScoreView"/> to <c>ScoreViews</c> (one row per score asset, deleted with the asset).</summary>
public sealed class ScoreViewConfiguration : IEntityTypeConfiguration<ScoreView>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ScoreView> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ScoreViews");
        builder.HasKey(view => view.AssetId);
        builder.Property(view => view.AssetId).ValueGeneratedNever();
        builder.HasOne<Asset>().WithOne().HasForeignKey<ScoreView>(view => view.AssetId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(view => view.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(view => view.WorkerJobId).HasMaxLength(32);
        builder.Property(view => view.Error).HasMaxLength(ScoreView.MaxErrorLength);
        builder.Property(view => view.Bars).HasColumnName("BarsJson")
            .HasConversion(JsonColumn<IReadOnlyList<ScoreBar>>.Converter, JsonColumn<IReadOnlyList<ScoreBar>>.Comparer);
        builder.HasIndex(view => view.Status);
        builder.HasIndex(view => view.TrackId);
    }
}

/// <summary>Maps <see cref="Setlist"/> to <c>Setlists</c>.</summary>
public sealed class SetlistConfiguration : IEntityTypeConfiguration<Setlist>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Setlist> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Setlists");
        builder.HasKey(setlist => setlist.Id);
        builder.Property(setlist => setlist.Id).ValueGeneratedNever();
        builder.Property(setlist => setlist.Name).HasMaxLength(SetlistRules.MaxNameLength);
        builder.Property(setlist => setlist.Note).HasMaxLength(SetlistRules.MaxNoteLength);
        builder.HasMany(setlist => setlist.Entries).WithOne().HasForeignKey(entry => entry.SetlistId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(setlist => setlist.Entries).HasField("_entries").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(setlist => setlist.UpdatedAt);
    }
}

/// <summary>Maps <see cref="SetlistEntry"/> to <c>SetlistEntries</c>: deleted with the track, mix set to null with the mix.</summary>
public sealed class SetlistEntryConfiguration : IEntityTypeConfiguration<SetlistEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SetlistEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("SetlistEntries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.HasIndex(entry => new { entry.SetlistId, entry.Position });
        builder.HasOne<Track>().WithMany().HasForeignKey(entry => entry.TrackId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Mix>().WithMany().HasForeignKey(entry => entry.MixId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(entry => entry.Note).HasMaxLength(SetlistRules.MaxEntryNoteLength);
    }
}

/// <summary>Maps <see cref="PracticeLogSettings"/> to <c>PracticeLogSettings</c> (seeded single row).</summary>
public sealed class PracticeLogSettingsConfiguration : IEntityTypeConfiguration<PracticeLogSettings>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PracticeLogSettings> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PracticeLogSettings");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).ValueGeneratedNever();
        builder.HasData(new { Id = PracticeLogSettings.SingletonId, Enabled = true, NoticeAcknowledged = false });
    }
}

/// <summary>
/// Maps <see cref="PracticeSession"/> to <c>PracticeSessions</c> (deleted with the track, mix set to null with the mix) and its
/// owned section rows to <c>PracticeSectionTimes</c>.
/// </summary>
public sealed class PracticeSessionConfiguration : IEntityTypeConfiguration<PracticeSession>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PracticeSession> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PracticeSessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();
        builder.HasOne<Track>().WithMany().HasForeignKey(session => session.TrackId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Mix>().WithMany().HasForeignKey(session => session.MixId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(session => new { session.TrackId, session.StartedAt });
        builder.HasIndex(session => session.StartedAt);
        builder.OwnsMany(session => session.Sections, SectionRows);
        builder.Navigation(session => session.Sections).HasField("_sections").UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void SectionRows(OwnedNavigationBuilder<PracticeSession, PracticeSectionTime> row)
    {
        row.ToTable("PracticeSectionTimes");
        row.WithOwner().HasForeignKey(time => time.SessionId);
        row.HasKey(time => new { time.SessionId, time.Index });
        row.Property(time => time.Index).ValueGeneratedNever();
        row.Property(time => time.Kind).HasConversion<string>().HasMaxLength(16);
        row.Property(time => time.Label).HasMaxLength(PracticeSessionRules.MaxLabelLength);
    }
}

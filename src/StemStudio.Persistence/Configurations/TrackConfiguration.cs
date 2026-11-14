using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StemStudio.Domain.Tracks;

namespace StemStudio.Persistence.Configurations;

/// <summary>Maps <see cref="Track"/>: unique source key, enums as strings, stems and beat grid as JSON columns.</summary>
public sealed class TrackConfiguration : IEntityTypeConfiguration<Track>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Track> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Tracks");
        builder.HasKey(track => track.Id);
        builder.Property(track => track.Id).ValueGeneratedNever();
        builder.Property(track => track.Key).HasColumnName("SourceKey").HasMaxLength(80)
            .HasConversion(key => key.Value, value => SourceKey.FromStored(value));
        builder.HasIndex(track => track.Key).IsUnique();
        builder.Property(track => track.SourceType).HasConversion<string>().HasMaxLength(16);
        builder.Property(track => track.Status).HasConversion<string>().HasMaxLength(16);
        ConfigureText(builder);
        builder.Ignore(track => track.Stems);
        builder.ComplexCollection<List<Stem>, Stem>("_stems", collection => collection.ToJson("Stems"));
        builder.ComplexProperty(track => track.BeatGrid, grid => grid.ToJson());
        builder.HasIndex(track => track.UpdatedAt);
    }

    private static void ConfigureText(EntityTypeBuilder<Track> builder)
    {
        builder.Property(track => track.SourceUrl).HasMaxLength(2048);
        builder.Property(track => track.Title).HasMaxLength(Track.MaxTitleLength);
        builder.Property(track => track.Artist).HasMaxLength(Track.MaxArtistLength);
        builder.Property(track => track.Stage).HasMaxLength(Track.MaxStageLength);
        builder.Property(track => track.Error).HasMaxLength(Track.MaxErrorLength);
        builder.Property(track => track.WorkerJobId).HasMaxLength(32);
        builder.Property(track => track.UploadFileName).HasMaxLength(80);
        builder.Property(track => track.Model).HasMaxLength(32);
    }
}

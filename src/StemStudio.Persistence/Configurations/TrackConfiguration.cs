using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StemStudio.Domain.Artists;
using StemStudio.Domain.Insights;
using StemStudio.Domain.Library;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;

namespace StemStudio.Persistence.Configurations;

/// <summary>Maps <see cref="Track"/>: unique source key, enums as strings, stems, beat grid, detail phase, device, artwork and lookup state as JSON columns; the artist reference is set null when the artist is deleted; the lyrics row (feature 006) is deleted with the track; library info (feature 022) as plain columns; the quality report (feature 020) as JSON.</summary>
public sealed class TrackConfiguration : IEntityTypeConfiguration<Track>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Track> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Tracks");
        builder.HasKey(track => track.Id);
        builder.Property(track => track.Id).ValueGeneratedNever();
        ConfigureKeyAndEnums(builder);
        ConfigureText(builder);
        builder.Ignore(track => track.Stems);
        builder.ComplexCollection<List<Stem>, Stem>("_stems", collection => collection.ToJson("Stems"));
        builder.ComplexProperty(track => track.BeatGrid, grid => grid.ToJson());
        builder.Property(track => track.DetailPhase).HasConversion(DetailPhaseJson.Converter, DetailPhaseJson.Comparer);
        builder.Property(track => track.Device).HasConversion(JobDeviceJson.Converter);
        builder.Property(track => track.Origin).HasConversion(JsonColumn<TrackOrigin>.Converter!, JsonColumn<TrackOrigin>.Comparer!);
        builder.Property(track => track.QualityReport).HasConversion(JsonColumn<QualityReport>.TolerantConverter!, JsonColumn<QualityReport>.Comparer!);
        ConfigureArtworkAndMetadata(builder);
        builder.ComplexProperty(track => track.LibraryInfo, LibraryInfoColumns);
    }

    /// <summary>The status column mirrors the row (read through the property, which prefers the loaded row); the row is deleted with the track.</summary>
    private static void ConfigureLyrics(EntityTypeBuilder<Track> builder)
    {
        builder.Property(track => track.LyricsStatus).HasConversion<string>().HasMaxLength(16).UsePropertyAccessMode(PropertyAccessMode.Property);
        builder.HasOne(track => track.Lyrics).WithOne().HasForeignKey<TrackLyrics>(lyrics => lyrics.TrackId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureArtworkAndMetadata(EntityTypeBuilder<Track> builder)
    {
        builder.Property(track => track.Artwork).HasConversion(TrackArtworkJson.Converter);
        builder.Property(track => track.AutoArtwork).HasConversion(TrackArtworkJson.Converter);
        builder.ComplexProperty(track => track.Metadata, metadata =>
        {
            metadata.ToJson();
            metadata.Property(value => value.Status).HasConversion<string>();
        });
        builder.HasOne<Artist>().WithMany().HasForeignKey(track => track.ArtistId).OnDelete(DeleteBehavior.SetNull);
        ConfigureLyrics(builder);
    }

    private static void ConfigureKeyAndEnums(EntityTypeBuilder<Track> builder)
    {
        builder.Property(track => track.Key).HasColumnName("SourceKey").HasMaxLength(80)
            .HasConversion(key => key.Value, value => SourceKey.FromStored(value));
        builder.HasIndex(track => track.Key).IsUnique();
        builder.HasIndex(track => track.UpdatedAt);
        builder.Property(track => track.SourceType).HasConversion<string>().HasMaxLength(16);
        builder.Property(track => track.Status).HasConversion<string>().HasMaxLength(16);
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

    /// <summary>Maps library info (feature 022) onto <c>LibraryArtist</c>, <c>LibraryAlbum</c>, <c>LibrarySong</c> (TEXT, at most 200).</summary>
    /// <param name="info">The complex property builder.</param>
    internal static void LibraryInfoColumns(ComplexPropertyBuilder<LibraryInfo> info)
    {
        info.Property(value => value.Artist).HasColumnName("LibraryArtist").HasMaxLength(LibraryInfoRules.MaxNameLength);
        info.Property(value => value.Album).HasColumnName("LibraryAlbum").HasMaxLength(LibraryInfoRules.MaxNameLength);
        info.Property(value => value.Song).HasColumnName("LibrarySong").HasMaxLength(LibraryInfoRules.MaxNameLength);
    }
}

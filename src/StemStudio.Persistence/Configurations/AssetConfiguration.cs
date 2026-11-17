using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Library;
using StemStudio.Domain.Scores;

namespace StemStudio.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Asset"/> (table <c>Assets</c>, feature 007): kind and status by name, files, audio facts, score facts (008) and provenance
/// as JSON, the source track as an indexed filter column (no foreign key: items outlive their track). Feature 022: the
/// placement and the item's library info as complex types on plain columns (so the backfill and the folder index can query
/// them); the folder index <c>IX_Assets_Folder</c> is created by the <c>AddLibraryFolders</c> migration.
/// </summary>
public sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Assets");
        builder.HasKey(asset => asset.Id);
        builder.Property(asset => asset.Id).ValueGeneratedNever();
        builder.Property(asset => asset.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(asset => asset.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(asset => asset.Name).HasMaxLength(AssetNaming.MaxNameLength);
        builder.Property(asset => asset.Error).HasMaxLength(Asset.MaxErrorLength);
        builder.Property(asset => asset.WorkerJobId).HasMaxLength(32);
        ConfigureJson(builder);
        ConfigureIndexes(builder);
        ConfigureLibrary(builder);
    }

    private static void ConfigureLibrary(EntityTypeBuilder<Asset> builder)
    {
        builder.ComplexProperty(asset => asset.Placement, placement =>
        {
            placement.ComplexProperty(value => value.Artist, level => Level(level, "Artist"));
            placement.ComplexProperty(value => value.Album, level => Level(level, "Album"));
            placement.ComplexProperty(value => value.Song, level => Level(level, "Song"));
            placement.Property(value => value.Year).HasColumnName("FolderYear");
            placement.HasDiscriminator();
        });
        builder.ComplexProperty(asset => asset.LibraryInfo, TrackConfiguration.LibraryInfoColumns);
        builder.HasIndex(asset => asset.FiledVersion);
    }

    private static void Level(ComplexPropertyBuilder<PlacementLevel> level, string name)
    {
        level.Property(value => value.Name).HasColumnName($"Folder{name}").HasMaxLength(LibraryInfoRules.MaxNameLength);
        level.Property(value => value.Key).HasColumnName($"Folder{name}Key").HasMaxLength(LibraryKey.MaxKeyLength);
        level.Property(value => value.Source).HasColumnName($"Folder{name}Source").HasMaxLength(16)
            .HasConversion(source => source.ToString().ToLowerInvariant(), text => Enum.Parse<PlacementSource>(text, true));
    }

    private static void ConfigureIndexes(EntityTypeBuilder<Asset> builder)
    {
        builder.HasIndex(asset => asset.CreatedAt);
        builder.HasIndex(asset => asset.TrackId);
        builder.HasIndex(asset => asset.Status);
    }

    private static void ConfigureJson(EntityTypeBuilder<Asset> builder)
    {
        builder.Ignore(asset => asset.Files);
        builder.Property(asset => asset.Score).HasConversion(JsonColumn<ScoreFacts>.Converter!, JsonColumn<ScoreFacts>.Comparer!);
        builder.Property<List<AssetFile>>("_files").HasColumnName("Files")
            .HasConversion(JsonColumn<List<AssetFile>>.Converter, JsonColumn<List<AssetFile>>.Comparer);
        builder.Property(asset => asset.Audio).HasConversion(JsonColumn<AudioFacts>.Converter!, JsonColumn<AudioFacts>.Comparer!);
        builder.Property(asset => asset.Provenance).HasConversion(JsonColumn<Provenance>.Converter, JsonColumn<Provenance>.Comparer);
    }
}

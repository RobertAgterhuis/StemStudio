using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StemStudio.Domain.Assets;

namespace StemStudio.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Asset"/> (table <c>Assets</c>, feature 007): kind and status by name, files, audio facts and provenance
/// as JSON, the source track as an indexed filter column (no foreign key: items outlive their track).
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
        builder.Property<List<AssetFile>>("_files").HasColumnName("Files")
            .HasConversion(JsonColumn<List<AssetFile>>.Converter, JsonColumn<List<AssetFile>>.Comparer);
        builder.Property(asset => asset.Audio).HasConversion(JsonColumn<AudioFacts>.Converter!, JsonColumn<AudioFacts>.Comparer!);
        builder.Property(asset => asset.Provenance).HasConversion(JsonColumn<Provenance>.Converter, JsonColumn<Provenance>.Comparer);
    }
}

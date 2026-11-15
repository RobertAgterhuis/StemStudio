using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StemStudio.Domain.Generations;

namespace StemStudio.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Generation"/> to <c>Generations</c> (feature 009, data-model 3.5): the details, seeds, takes,
/// warnings, device and error are JSON columns (the takes are owned by the row and deleted with it).
/// </summary>
public sealed class GenerationConfiguration : IEntityTypeConfiguration<Generation>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Generation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Generations");
        builder.HasKey(generation => generation.Id);
        builder.Property(generation => generation.Id).ValueGeneratedNever();
        builder.Property(generation => generation.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(generation => generation.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(generation => generation.Stage).HasMaxLength(Generation.MaxStageLength);
        builder.Property(generation => generation.GeneratorJobId).HasMaxLength(32);
        builder.HasIndex(generation => generation.SourceTrackId);
        builder.HasIndex(generation => generation.Status);
        builder.HasIndex(generation => generation.CreatedAt);
        ConfigureJson(builder);
    }

    private static void ConfigureJson(EntityTypeBuilder<Generation> builder)
    {
        builder.Property(generation => generation.Restyle).HasConversion(JsonColumn<RestyleDetails>.Converter!, JsonColumn<RestyleDetails>.Comparer!);
        builder.Property(generation => generation.Error).HasConversion(JsonColumn<GenerationError>.Converter!, JsonColumn<GenerationError>.Comparer!);
        builder.Property(generation => generation.Device).HasConversion(JobDeviceJson.Converter);
        builder.Ignore(generation => generation.Seeds).Ignore(generation => generation.Takes).Ignore(generation => generation.Warnings);
        builder.Ignore(generation => generation.LibraryAssetIds);
        JsonList<int>(builder, "_seeds", "Seeds");
        JsonList<GenerationTake>(builder, "_takes", "Takes");
        JsonList<string>(builder, "_warnings", "Warnings");
    }

    private static void JsonList<T>(EntityTypeBuilder<Generation> builder, string field, string column) =>
        builder.Property<List<T>>(field).HasColumnName(column).HasConversion(JsonColumn<List<T>>.Converter, JsonColumn<List<T>>.Comparer);
}

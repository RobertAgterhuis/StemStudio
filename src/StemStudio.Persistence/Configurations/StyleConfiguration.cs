using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StemStudio.Domain.Styles;

namespace StemStudio.Persistence.Configurations;

/// <summary>Maps <see cref="Style"/> to <c>Styles</c> (feature 009, data-model 3.5): tags as JSON, unique upper-case name.</summary>
public sealed class StyleConfiguration : IEntityTypeConfiguration<Style>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Style> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Styles");
        builder.HasKey(style => style.Id);
        builder.Property(style => style.Id).ValueGeneratedNever();
        builder.Property(style => style.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(style => style.PresetKey).HasMaxLength(40);
        builder.Property(style => style.Name).HasMaxLength(StyleRules.MaxNameLength);
        builder.Property(style => style.NormalizedName).HasMaxLength(StyleRules.MaxNameLength);
        builder.HasIndex(style => style.NormalizedName).IsUnique();
        builder.Property(style => style.SubGenre).HasMaxLength(StyleRules.MaxSubGenreLength);
        builder.Property(style => style.Description).HasMaxLength(StyleRules.MaxDescriptionLength);
        ConfigureEnums(builder);
        builder.Ignore(style => style.Instrumentation).Ignore(style => style.Moods).Ignore(style => style.Fields);
        builder.Property<List<string>>("_instrumentation").HasColumnName("Instrumentation")
            .HasConversion(JsonColumn<List<string>>.Converter, JsonColumn<List<string>>.Comparer);
        builder.Property<List<string>>("_moods").HasColumnName("Moods")
            .HasConversion(JsonColumn<List<string>>.Converter, JsonColumn<List<string>>.Comparer);
    }

    private static void ConfigureEnums(EntityTypeBuilder<Style> builder)
    {
        builder.Property(style => style.GenreFamily).HasConversion<string>().HasMaxLength(16);
        builder.Property(style => style.TempoFeel).HasConversion<string>().HasMaxLength(16);
        builder.Property(style => style.VocalHandling).HasConversion<string>().HasMaxLength(16);
        builder.Property(style => style.DefaultStrength).HasConversion<string>().HasMaxLength(16);
    }
}

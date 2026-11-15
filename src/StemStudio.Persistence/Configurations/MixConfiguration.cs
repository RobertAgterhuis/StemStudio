using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Tracks;

namespace StemStudio.Persistence.Configurations;

/// <summary>Maps <see cref="Mix"/>: unique (TrackId, NormalizedName), restrict FK to the track, settings as JSON columns.</summary>
public sealed class MixConfiguration : IEntityTypeConfiguration<Mix>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Mix> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Mixes");
        builder.HasKey(mix => mix.Id);
        builder.Property(mix => mix.Id).ValueGeneratedNever();
        builder.Property(mix => mix.Name).HasMaxLength(MixSettings.MaxNameLength);
        builder.Property(mix => mix.NormalizedName).HasMaxLength(MixSettings.MaxNameLength);
        builder.HasIndex(mix => new { mix.TrackId, mix.NormalizedName }).IsUnique();
        builder.HasIndex(mix => mix.UpdatedAt);
        builder.HasOne<Track>().WithMany().HasForeignKey(mix => mix.TrackId).OnDelete(DeleteBehavior.Restrict);
        ConfigureSettings(builder);
    }

    private static void ConfigureSettings(EntityTypeBuilder<Mix> builder)
    {
        builder.Ignore(mix => mix.Channels);
        builder.ComplexCollection<List<ChannelSettings>, ChannelSettings>("_channels", collection => collection.ToJson("Channels"));
        builder.ComplexProperty(mix => mix.Metronome, metronome => metronome.ToJson());
        builder.ComplexProperty(mix => mix.Loop, loop => loop.ToJson());
    }
}

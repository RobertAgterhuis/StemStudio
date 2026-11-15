using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StemStudio.Domain.Lyrics;

namespace StemStudio.Persistence.Configurations;

/// <summary>
/// Maps <see cref="TrackLyrics"/> (table <c>TrackLyrics</c>, feature 006): one row per track, deleted with it; the
/// document as a JSON column, the alignment device as the 003 device JSON, the status by name.
/// </summary>
public sealed class TrackLyricsConfiguration : IEntityTypeConfiguration<TrackLyrics>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TrackLyrics> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("TrackLyrics");
        builder.HasKey(lyrics => lyrics.TrackId);
        builder.Property(lyrics => lyrics.TrackId).ValueGeneratedNever();
        builder.Property(lyrics => lyrics.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(lyrics => lyrics.Reason).HasMaxLength(TrackLyrics.MaxReasonLength);
        builder.Property(lyrics => lyrics.LyricsJobId).HasMaxLength(32);
        builder.Property(lyrics => lyrics.Document).HasConversion(JsonColumn<LyricsDocument>.Converter!, JsonColumn<LyricsDocument>.Comparer!);
        builder.Property(lyrics => lyrics.AlignDevice).HasConversion(JobDeviceJson.Converter);
    }
}

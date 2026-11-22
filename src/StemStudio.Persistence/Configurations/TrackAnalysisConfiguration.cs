using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Tracks;

namespace StemStudio.Persistence.Configurations;

/// <summary>
/// Maps <see cref="TrackAnalysis"/> to <c>TrackAnalyses</c> (feature 016, data-model 7.1): key <c>(TrackId, Kind)</c> (one
/// analysis per kind and track), deleted with the track; the document is one JSON column.
/// </summary>
public sealed class TrackAnalysisConfiguration : IEntityTypeConfiguration<TrackAnalysis>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TrackAnalysis> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("TrackAnalyses");
        builder.HasKey(analysis => new { analysis.TrackId, analysis.Kind });
        builder.HasOne<Track>().WithMany().HasForeignKey(analysis => analysis.TrackId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(analysis => analysis.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(analysis => analysis.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(analysis => analysis.Reason).HasMaxLength(TrackAnalysis.MaxReasonLength);
        builder.Property(analysis => analysis.JobId).HasMaxLength(32);
        builder.Property(analysis => analysis.Engine).HasMaxLength(32);
        builder.Property(analysis => analysis.SourceFingerprint).HasMaxLength(TrackAnalysis.FingerprintLength);
        ConfigureJsonColumns(builder);
        builder.HasIndex(analysis => analysis.Status);
    }

    /// <summary>Options, document and device as JSON columns.</summary>
    private static void ConfigureJsonColumns(EntityTypeBuilder<TrackAnalysis> builder)
    {
        builder.Property(analysis => analysis.Options).HasColumnName("OptionsJson")
            .HasConversion(JsonColumn<AnalysisOptions>.Converter, JsonColumn<AnalysisOptions>.Comparer);
        builder.Property(analysis => analysis.Document).HasColumnName("DocumentJson")
            .HasConversion(AnalysisDocumentJson.Converter, AnalysisDocumentJson.Comparer);
        builder.Property(analysis => analysis.Device).HasColumnName("DeviceJson").HasConversion(JobDeviceJson.Converter);
    }
}

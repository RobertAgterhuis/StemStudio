using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Recording;
using StemStudio.Domain.Tracks;

namespace StemStudio.Persistence.Configurations;

/// <summary>
/// Maps <see cref="RecordingTake"/> (table <c>RecordingTakes</c>, feature 015 data-model 2.1): enums by name, cascade
/// delete with the track and with its asset, set null when its mix or calibration is deleted; unique
/// <c>(TrackId, Number)</c> and <c>AssetId</c>.
/// </summary>
public sealed class RecordingTakeConfiguration : IEntityTypeConfiguration<RecordingTake>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RecordingTake> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("RecordingTakes");
        builder.HasKey(take => take.Id);
        builder.Property(take => take.Id).ValueGeneratedNever();
        builder.Property(take => take.Name).HasMaxLength(RecordingConstants.MaxNameLength);
        builder.Property(take => take.DeviceLabel).HasMaxLength(RecordingConstants.MaxDeviceLabelLength);
        builder.Property(take => take.Part).HasConversion<string>().HasMaxLength(16);
        builder.Property(take => take.InputMode).HasConversion<string>().HasMaxLength(16);
        builder.Property(take => take.LatencyMethod).HasConversion<string>().HasMaxLength(16);
        builder.Ignore(take => take.Quiet);
        builder.HasOne<Track>().WithMany().HasForeignKey(take => take.TrackId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Asset>().WithOne().HasForeignKey<RecordingTake>(take => take.AssetId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Mix>().WithMany().HasForeignKey(take => take.MixId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<InputCalibration>().WithMany().HasForeignKey(take => take.CalibrationId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(take => new { take.TrackId, take.Number }).IsUnique();
        builder.HasIndex(take => new { take.TrackId, take.CreatedAt }).IsDescending(false, true);
        builder.HasIndex(take => new { take.TrackId, take.SessionId });
        builder.HasIndex(take => take.AssetId).IsUnique();
    }
}

/// <summary>Maps <see cref="InputCalibration"/> (table <c>InputCalibrations</c>, data-model 2.5): unique device label, method by name.</summary>
public sealed class InputCalibrationConfiguration : IEntityTypeConfiguration<InputCalibration>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<InputCalibration> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("InputCalibrations");
        builder.HasKey(calibration => calibration.Id);
        builder.Property(calibration => calibration.Id).ValueGeneratedNever();
        builder.Property(calibration => calibration.DeviceLabel).HasMaxLength(RecordingConstants.MaxDeviceLabelLength);
        builder.Property(calibration => calibration.DeviceIdHint).HasMaxLength(RecordingConstants.MaxDeviceIdHintLength);
        builder.Property(calibration => calibration.Method).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(calibration => calibration.DeviceLabel).IsUnique();
    }
}

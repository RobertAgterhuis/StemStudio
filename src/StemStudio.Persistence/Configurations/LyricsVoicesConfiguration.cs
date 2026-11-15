using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Voices;

namespace StemStudio.Persistence.Configurations;

/// <summary>Maps <see cref="LyricsSheet"/> to <c>LyricsSheets</c> (feature 010).</summary>
public sealed class LyricsSheetConfiguration : IEntityTypeConfiguration<LyricsSheet>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<LyricsSheet> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("LyricsSheets");
        builder.HasKey(sheet => sheet.Id);
        builder.Property(sheet => sheet.Id).ValueGeneratedNever();
        builder.Property(sheet => sheet.Title).HasMaxLength(LyricsRules.MaxTitleLength);
        builder.Property(sheet => sheet.Language).HasMaxLength(8);
        builder.Property(sheet => sheet.Text).HasMaxLength(LyricsRules.MaxTextLength);
        builder.HasIndex(sheet => sheet.UpdatedAt);
    }
}

/// <summary>Maps <see cref="VoiceProfile"/> to <c>VoiceProfiles</c> (feature 010): consent as a JSON column, unique upper-case name.</summary>
public sealed class VoiceProfileConfiguration : IEntityTypeConfiguration<VoiceProfile>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VoiceProfile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("VoiceProfiles");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.Id).ValueGeneratedNever();
        builder.Property(profile => profile.DisplayName).HasMaxLength(VoiceProfile.MaxDisplayNameLength);
        builder.Property(profile => profile.NormalizedName).HasMaxLength(VoiceProfile.MaxDisplayNameLength);
        builder.HasIndex(profile => profile.NormalizedName).IsUnique();
        builder.Property(profile => profile.SampleSource).HasConversion<string>().HasMaxLength(16);
        builder.Property(profile => profile.SamplePath).HasMaxLength(200);
        builder.Property(profile => profile.SamplePcmSha256).HasMaxLength(64);
        builder.Property(profile => profile.SampleFileSha256).HasMaxLength(64);
        builder.Property(profile => profile.Consent).HasConversion(JsonColumn<VoiceConsent>.Converter, JsonColumn<VoiceConsent>.Comparer);
    }
}

/// <summary>Maps <see cref="AudioHash"/> to <c>AudioHashes</c> (Sha256 primary key).</summary>
public sealed class AudioHashConfiguration : IEntityTypeConfiguration<AudioHash>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AudioHash> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("AudioHashes");
        builder.HasKey(hash => hash.Sha256);
        builder.Property(hash => hash.Sha256).HasMaxLength(64);
        builder.Property(hash => hash.Kind).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(hash => hash.OwnerId);
    }
}

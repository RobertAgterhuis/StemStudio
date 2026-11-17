using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Persistence.Configurations;

/// <summary>Maps <see cref="AssistRun"/> to <c>LyricAssists</c> (feature 023); deleted with the sheet.</summary>
public sealed class LyricAssistConfiguration : IEntityTypeConfiguration<AssistRun>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AssistRun> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("LyricAssists");
        builder.HasKey(assist => assist.Id);
        builder.Property(assist => assist.Id).ValueGeneratedNever();
        builder.HasOne<LyricsSheet>().WithMany().HasForeignKey(assist => assist.SheetId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(assist => new { assist.SheetId, assist.CreatedAt });
        builder.HasIndex(assist => assist.Status);
        foreach (var name in new[] { nameof(AssistRun.Kind), nameof(AssistRun.Sensitivity), nameof(AssistRun.Language), nameof(AssistRun.Status), nameof(AssistRun.Origin), nameof(AssistRun.FailureCode) })
        {
            builder.Property(name).HasConversion<string>().HasMaxLength(24);
        }

        builder.Property(assist => assist.Instruction).HasMaxLength(200);
        builder.Property(assist => assist.Backend).HasMaxLength(16);
        builder.Property(assist => assist.Model).HasMaxLength(100);
        builder.Property(assist => assist.FailureMessage).HasMaxLength(500);
        builder.Property(assist => assist.FailureFix).HasMaxLength(500);
        builder.Property(assist => assist.Categories).HasConversion(JsonColumn<IReadOnlyList<SuggestionCategory>>.Converter, JsonColumn<IReadOnlyList<SuggestionCategory>>.Comparer);
        builder.Property(assist => assist.Layers).HasConversion(JsonColumn<IReadOnlyList<AssistLayer>>.Converter, JsonColumn<IReadOnlyList<AssistLayer>>.Comparer);
        builder.Property(assist => assist.Counts).HasConversion(JsonColumn<CategoryCounts>.Converter, JsonColumn<CategoryCounts>.Comparer);
        builder.Property(assist => assist.Discarded).HasConversion(JsonColumn<DiscardedCounts>.Converter, JsonColumn<DiscardedCounts>.Comparer);
        builder.Property(assist => assist.Usage).HasConversion(JsonColumn<AssistUsage>.Converter!, JsonColumn<AssistUsage>.Comparer!);
    }
}

/// <summary>Maps <see cref="LyricSuggestion"/> to <c>LyricSuggestions</c>; deleted with its assist and its sheet.</summary>
public sealed class LyricSuggestionConfiguration : IEntityTypeConfiguration<LyricSuggestion>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<LyricSuggestion> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("LyricSuggestions");
        builder.HasKey(suggestion => suggestion.Id);
        builder.Property(suggestion => suggestion.Id).ValueGeneratedNever();
        builder.HasOne<AssistRun>().WithMany().HasForeignKey(suggestion => suggestion.AssistId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<LyricsSheet>().WithMany().HasForeignKey(suggestion => suggestion.SheetId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(suggestion => new { suggestion.SheetId, suggestion.Status });
        foreach (var name in new[] { nameof(LyricSuggestion.Category), nameof(LyricSuggestion.Type), nameof(LyricSuggestion.Severity), nameof(LyricSuggestion.Operation), nameof(LyricSuggestion.Source), nameof(LyricSuggestion.Status) })
        {
            builder.Property(name).HasConversion<string>().HasMaxLength(32);
        }

        builder.Property(suggestion => suggestion.Proposal).HasMaxLength(LyricSuggestion.MaxProposalLength);
        builder.Property(suggestion => suggestion.Explanation).HasMaxLength(LyricSuggestion.MaxExplanationLength);
        builder.Property(suggestion => suggestion.Model).HasMaxLength(100);
        builder.Property(suggestion => suggestion.Anchor).HasConversion(JsonColumn<Anchor>.Converter, JsonColumn<Anchor>.Comparer);
        builder.Property(suggestion => suggestion.RhymeWith).HasConversion(JsonColumn<RhymePartner>.Converter!, JsonColumn<RhymePartner>.Comparer!);
    }
}

/// <summary>Maps <see cref="LyricsSheetRevision"/> to <c>LyricsSheetRevisions</c> (key sheet + revision).</summary>
public sealed class LyricsSheetRevisionConfiguration : IEntityTypeConfiguration<LyricsSheetRevision>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<LyricsSheetRevision> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("LyricsSheetRevisions");
        builder.HasKey(revision => new { revision.SheetId, revision.Revision });
        builder.HasOne<LyricsSheet>().WithMany().HasForeignKey(revision => revision.SheetId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(revision => revision.Title).HasMaxLength(LyricsRules.MaxTitleLength);
        builder.Property(revision => revision.Text).HasMaxLength(LyricsRules.MaxTextLength);
        builder.Property(revision => revision.Cause).HasConversion<string>().HasMaxLength(16);
    }
}

/// <summary>Maps <see cref="AssistEvent"/> to <c>LyricAssistEvents</c> (key assist + seq).</summary>
public sealed class LyricAssistEventConfiguration : IEntityTypeConfiguration<AssistEvent>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AssistEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("LyricAssistEvents");
        builder.HasKey(e => new { e.AssistId, e.Seq });
        builder.HasOne<AssistRun>().WithMany().HasForeignKey(e => e.AssistId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(e => e.Type).HasMaxLength(32);
        builder.Property(e => e.Data).HasMaxLength(AssistEvent.MaxPayloadBytes);
    }
}

/// <summary>Maps <see cref="LyricWord"/> to <c>LyricWords</c>, unique per language.</summary>
public sealed class LyricWordConfiguration : IEntityTypeConfiguration<LyricWord>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<LyricWord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("LyricWords");
        builder.HasKey(word => word.Id);
        builder.Property(word => word.Id).ValueGeneratedNever();
        builder.Property(word => word.Language).HasConversion<string>().HasMaxLength(16);
        builder.Property(word => word.Word).HasMaxLength(LyricWord.MaxLength);
        builder.HasIndex(word => new { word.Language, word.Word }).IsUnique();
    }
}

/// <summary>Maps <see cref="LyricAssistSettings"/> to <c>LyricAssistSettings</c> (one row).</summary>
public sealed class LyricAssistSettingsConfiguration : IEntityTypeConfiguration<LyricAssistSettings>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<LyricAssistSettings> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("LyricAssistSettings");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).ValueGeneratedNever();
        builder.Property(settings => settings.ClaudeNoticeVersion).HasMaxLength(40);
    }
}

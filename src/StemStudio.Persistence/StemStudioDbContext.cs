using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Artists;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Generations;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Styles;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Voices;
using StemStudio.Persistence.Agent;
using StemStudio.Persistence.Configurations;

namespace StemStudio.Persistence;

/// <summary>
/// EF Core context for tracks, stems and mixes (SQLite). Entity configurations live in this
/// assembly and are applied automatically; see specs/001-stemstudio-mvp/data-model.md.
/// </summary>
/// <param name="options">Context options (provider and connection string).</param>
public sealed class StemStudioDbContext(DbContextOptions<StemStudioDbContext> options) : DbContext(options)
{
    /// <summary>Imported tracks.</summary>
    public DbSet<Track> Tracks => Set<Track>();

    /// <summary>Saved mixes.</summary>
    public DbSet<Mix> Mixes => Set<Mix>();

    /// <summary>Artists matched by online lookups (feature 005).</summary>
    public DbSet<Artist> Artists => Set<Artist>();

    /// <summary>Library items (feature 007).</summary>
    public DbSet<Asset> Assets => Set<Asset>();

    /// <summary>The style library (feature 009).</summary>
    public DbSet<Style> Styles => Set<Style>();

    /// <summary>Generations: restyles (feature 009), songs and voice conversions (010).</summary>
    public DbSet<Generation> Generations => Set<Generation>();

    /// <summary>Own lyrics sheets (feature 010).</summary>
    public DbSet<LyricsSheet> LyricsSheets => Set<LyricsSheet>();

    /// <summary>Own-voice profiles (feature 010).</summary>
    public DbSet<VoiceProfile> VoiceProfiles => Set<VoiceProfile>();

    /// <summary>File-byte hashes of stored audio (feature 010 exact-copy guard).</summary>
    public DbSet<AudioHash> AudioHashes => Set<AudioHash>();

    /// <summary>The agent settings row (feature 011).</summary>
    public DbSet<AgentSettingsEntity> AgentSettings => Set<AgentSettingsEntity>();

    /// <summary>Agent conversations (feature 011).</summary>
    public DbSet<ConversationEntity> AgentConversations => Set<ConversationEntity>();

    /// <summary>Agent conversation messages (feature 011).</summary>
    public DbSet<ConversationMessageEntity> AgentMessages => Set<ConversationMessageEntity>();

    /// <summary>Agent runs (feature 011).</summary>
    public DbSet<AgentRunEntity> AgentRuns => Set<AgentRunEntity>();

    /// <summary>Agent run steps (feature 011).</summary>
    public DbSet<RunStepEntity> AgentRunSteps => Set<RunStepEntity>();

    /// <summary>Agent approvals (feature 011).</summary>
    public DbSet<ApprovalEntity> AgentApprovals => Set<ApprovalEntity>();

    /// <summary>Agent undo entries (feature 011).</summary>
    public DbSet<UndoEntryEntity> AgentUndoEntries => Set<UndoEntryEntity>();

    /// <summary>Agent run events (feature 011, SSE replay).</summary>
    public DbSet<RunEventEntity> AgentRunEvents => Set<RunEventEntity>();

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StemStudioDbContext).Assembly);
    }
}

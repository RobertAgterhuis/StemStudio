using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StemStudio.Persistence.Agent;

namespace StemStudio.Persistence.Configurations;

/// <summary>Maps <see cref="AgentSettingsEntity"/> to <c>AgentSettings</c> (feature 011, one row).</summary>
public sealed class AgentSettingsConfiguration : IEntityTypeConfiguration<AgentSettingsEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AgentSettingsEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("AgentSettings");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).ValueGeneratedNever();
        builder.Property(settings => settings.Backend).HasMaxLength(16);
        builder.Property(settings => settings.OllamaModel).HasMaxLength(100);
        builder.Property(settings => settings.OllamaFallbackModel).HasMaxLength(100);
        builder.Property(settings => settings.ClaudeModel).HasMaxLength(32);
        builder.Property(settings => settings.ClaudeEffort).HasMaxLength(16);
        builder.Property(settings => settings.Limits).HasMaxLength(1024);
    }
}

/// <summary>Maps <see cref="ConversationEntity"/> to <c>Conversations</c>.</summary>
public sealed class ConversationConfiguration : IEntityTypeConfiguration<ConversationEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ConversationEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Conversations");
        builder.HasKey(conversation => conversation.Id);
        builder.Property(conversation => conversation.Id).ValueGeneratedNever();
        builder.Property(conversation => conversation.Title).HasMaxLength(120);
        builder.HasIndex(conversation => conversation.UpdatedAt);
    }
}

/// <summary>Maps <see cref="ConversationMessageEntity"/> to <c>ConversationMessages</c> (unique Seq per conversation).</summary>
public sealed class ConversationMessageConfiguration : IEntityTypeConfiguration<ConversationMessageEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ConversationMessageEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ConversationMessages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.Role).HasMaxLength(16);
        builder.Property(message => message.Text).HasMaxLength(16000);
        builder.Property(message => message.Page).HasMaxLength(40);
        builder.HasIndex(message => new { message.ConversationId, message.Seq }).IsUnique();
        builder.HasOne<ConversationEntity>().WithMany().HasForeignKey(message => message.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Maps <see cref="AgentRunEntity"/> to <c>AgentRuns</c>.</summary>
public sealed class AgentRunConfiguration : IEntityTypeConfiguration<AgentRunEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AgentRunEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("AgentRuns");
        builder.HasKey(run => run.Id);
        builder.Property(run => run.Id).ValueGeneratedNever();
        builder.Property(run => run.Status).HasMaxLength(24);
        builder.Property(run => run.Backend).HasMaxLength(16);
        builder.Property(run => run.Model).HasMaxLength(100);
        builder.Property(run => run.FailureCode).HasMaxLength(40);
        builder.Property(run => run.FailureMessage).HasMaxLength(500);
        builder.HasIndex(run => run.Status);
        builder.HasIndex(run => new { run.ConversationId, run.StartedAt });
        builder.HasOne<ConversationEntity>().WithMany().HasForeignKey(run => run.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Maps <see cref="RunStepEntity"/> to <c>RunSteps</c> (unique Seq per run).</summary>
public sealed class RunStepConfiguration : IEntityTypeConfiguration<RunStepEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RunStepEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("RunSteps");
        builder.HasKey(step => step.Id);
        builder.Property(step => step.Id).ValueGeneratedNever();
        builder.Property(step => step.CallId).HasMaxLength(100);
        builder.Property(step => step.Tool).HasMaxLength(64);
        builder.Property(step => step.Arguments).HasMaxLength(8192);
        builder.Property(step => step.ArgumentsHash).HasMaxLength(64);
        builder.Property(step => step.Error).HasMaxLength(500);
        builder.Property(step => step.JobId).HasMaxLength(64);
        builder.HasIndex(step => new { step.RunId, step.Seq }).IsUnique();
        builder.HasOne<AgentRunEntity>().WithMany().HasForeignKey(step => step.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Maps <see cref="ApprovalEntity"/> to <c>Approvals</c>.</summary>
public sealed class ApprovalConfiguration : IEntityTypeConfiguration<ApprovalEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ApprovalEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Approvals");
        builder.HasKey(approval => approval.Id);
        builder.Property(approval => approval.Id).ValueGeneratedNever();
        builder.Property(approval => approval.Status).HasMaxLength(16);
        builder.Property(approval => approval.Scope).HasMaxLength(16);
        builder.Property(approval => approval.ArgumentsHash).HasMaxLength(64);
        builder.HasIndex(approval => new { approval.RunId, approval.Status });
        builder.HasOne<AgentRunEntity>().WithMany().HasForeignKey(approval => approval.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Maps <see cref="UndoEntryEntity"/> to <c>UndoEntries</c>.</summary>
public sealed class UndoEntryConfiguration : IEntityTypeConfiguration<UndoEntryEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UndoEntryEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("UndoEntries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.Kind).HasMaxLength(16);
        builder.Property(entry => entry.ActionTool).HasMaxLength(64);
        builder.Property(entry => entry.ActionArguments).HasMaxLength(8192);
        builder.Property(entry => entry.Status).HasMaxLength(16);
        builder.HasIndex(entry => new { entry.ConversationId, entry.Ordinal });
        builder.HasOne<AgentRunEntity>().WithMany().HasForeignKey(entry => entry.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Maps <see cref="RunEventEntity"/> to <c>RunEvents</c> (key RunId + Seq).</summary>
public sealed class RunEventConfiguration : IEntityTypeConfiguration<RunEventEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RunEventEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("RunEvents");
        builder.HasKey(runEvent => new { runEvent.RunId, runEvent.Seq });
        builder.Property(runEvent => runEvent.Seq).ValueGeneratedNever();
        builder.Property(runEvent => runEvent.Type).HasMaxLength(32);
        builder.Property(runEvent => runEvent.Payload).HasMaxLength(16384);
        builder.HasOne<AgentRunEntity>().WithMany().HasForeignKey(runEvent => runEvent.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StemStudio.Persistence.Agent;

namespace StemStudio.Persistence.Configurations;

/// <summary>
/// Maps <see cref="AgentSecretEntity"/> to <c>AgentSecrets</c> (011 follow-up claude-key). SQLite ignores declared lengths, so
/// the limits are also check constraints.
/// </summary>
public sealed class AgentSecretConfiguration : IEntityTypeConfiguration<AgentSecretEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AgentSecretEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("AgentSecrets", table =>
        {
            table.HasCheckConstraint("CK_AgentSecrets_Name", $"length(\"Name\") BETWEEN 1 AND {AgentSecretEntity.NameMaxLength}");
            table.HasCheckConstraint("CK_AgentSecrets_Ciphertext", $"length(\"Ciphertext\") BETWEEN 1 AND {AgentSecretEntity.CiphertextMaxLength}");
            table.HasCheckConstraint("CK_AgentSecrets_LastFour", $"length(\"LastFour\") = {AgentSecretEntity.LastFourLength}");
        });
        builder.HasKey(secret => secret.Name);
        builder.Property(secret => secret.Name).HasMaxLength(AgentSecretEntity.NameMaxLength).ValueGeneratedNever();
        builder.Property(secret => secret.Ciphertext).HasMaxLength(AgentSecretEntity.CiphertextMaxLength);
        builder.Property(secret => secret.LastFour).HasMaxLength(AgentSecretEntity.LastFourLength).IsFixedLength();
    }
}

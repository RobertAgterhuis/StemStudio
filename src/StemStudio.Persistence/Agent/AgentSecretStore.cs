using Microsoft.EntityFrameworkCore;

namespace StemStudio.Persistence.Agent;

/// <summary>EF access to the <c>AgentSecrets</c> table (011 follow-up claude-key, data-model 4). Stores ciphertext only.</summary>
public interface IAgentSecretStore
{
    /// <summary>The row of a secret, untracked; null when none is stored.</summary>
    /// <param name="name">Secret name.</param>
    /// <returns>The row or null.</returns>
    AgentSecretEntity? Find(string name);

    /// <summary>Inserts the row or replaces <c>Ciphertext</c>, <c>LastFour</c>, <c>PurposeVersion</c> and <c>StoredAt</c> of the existing one, in one transaction.</summary>
    /// <param name="secret">The row to save.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when an existing row was replaced.</returns>
    Task<bool> UpsertAsync(AgentSecretEntity secret, CancellationToken cancellationToken);

    /// <summary>Re-protection after a key-ring rotation: updates only <c>Ciphertext</c> and <c>StoredAt</c>.</summary>
    /// <param name="name">Secret name.</param>
    /// <param name="ciphertext">New ciphertext.</param>
    /// <param name="storedAt">Time of the re-protection (UTC).</param>
    /// <returns>False when the row no longer exists.</returns>
    bool Reprotect(string name, string ciphertext, DateTime storedAt);

    /// <summary>Removes the row; idempotent.</summary>
    /// <param name="name">Secret name.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when a row was removed.</returns>
    Task<bool> DeleteAsync(string name, CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="IAgentSecretStore"/>.</summary>
public sealed class AgentSecretStore : IAgentSecretStore
{
    private readonly StemStudioDbContext _context;

    /// <summary>Creates the store over a context.</summary>
    /// <param name="context">The database context.</param>
    public AgentSecretStore(StemStudioDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public AgentSecretEntity? Find(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _context.AgentSecrets.AsNoTracking().SingleOrDefault(secret => secret.Name == name);
    }

    /// <inheritdoc />
    public async Task<bool> UpsertAsync(AgentSecretEntity secret, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(secret);
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var existing = await _context.AgentSecrets.SingleOrDefaultAsync(row => row.Name == secret.Name, cancellationToken);
        var target = existing ?? _context.AgentSecrets.Add(new AgentSecretEntity { Name = secret.Name }).Entity;
        target.Ciphertext = secret.Ciphertext;
        target.LastFour = secret.LastFour;
        target.PurposeVersion = secret.PurposeVersion;
        target.StoredAt = secret.StoredAt;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _context.ChangeTracker.Clear();
            throw;
        }

        await transaction.CommitAsync(cancellationToken);
        return existing is not null;
    }

    /// <inheritdoc />
    public bool Reprotect(string name, string ciphertext, DateTime storedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(ciphertext);
        return _context.AgentSecrets.Where(secret => secret.Name == name).ExecuteUpdate(setters => setters
            .SetProperty(secret => secret.Ciphertext, ciphertext)
            .SetProperty(secret => secret.StoredAt, storedAt)) > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return await _context.AgentSecrets.Where(secret => secret.Name == name).ExecuteDeleteAsync(cancellationToken) > 0;
    }
}

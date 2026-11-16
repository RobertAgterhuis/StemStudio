using StemStudio.Persistence.Agent;

namespace StemStudio.Api.Agent.Secrets;

/// <summary>
/// The <c>claude-api-key</c> row of <c>AgentSecrets</c>, each operation in its own scope so the singleton key store can use it
/// (011 follow-up claude-key).
/// </summary>
/// <param name="scopes">The scope factory.</param>
public sealed class ClaudeKeyRow(IServiceScopeFactory scopes)
{
    private const string Name = AgentSecretEntity.ClaudeApiKeyName;

    /// <summary>The row, or null.</summary>
    /// <returns>The row.</returns>
    public AgentSecretEntity? Find()
    {
        using var scope = scopes.CreateScope();
        return Secrets(scope).Find(Name);
    }

    /// <summary>Inserts or replaces the row in one transaction.</summary>
    /// <param name="ciphertext">Data Protection payload.</param>
    /// <param name="lastFour">Last four characters of the key.</param>
    /// <param name="storedAt">Save time (UTC).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when a row was replaced.</returns>
    public async Task<bool> UpsertAsync(string ciphertext, string lastFour, DateTime storedAt, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var secret = new AgentSecretEntity
        {
            Name = Name,
            Ciphertext = ciphertext,
            LastFour = lastFour,
            PurposeVersion = ProtectedClaudeKeyStore.PurposeVersion,
            StoredAt = storedAt,
        };
        return await Secrets(scope).UpsertAsync(secret, cancellationToken);
    }

    /// <summary>Saves a re-protected ciphertext.</summary>
    /// <param name="ciphertext">New payload.</param>
    /// <param name="storedAt">Time of the re-protection (UTC).</param>
    /// <returns>False when the row was deleted meanwhile.</returns>
    public bool Reprotect(string ciphertext, DateTime storedAt)
    {
        using var scope = scopes.CreateScope();
        return Secrets(scope).Reprotect(Name, ciphertext, storedAt);
    }

    /// <summary>Removes the row; idempotent.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when a row was removed.</returns>
    public async Task<bool> DeleteAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await Secrets(scope).DeleteAsync(Name, cancellationToken);
    }

    private static IAgentSecretStore Secrets(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<IAgentSecretStore>();
}

using StemStudio.Agent.Model;

namespace StemStudio.Agent.Ports;

/// <summary>
/// Write side of the stored Claude key (data-model 4), used only by the key endpoints. Implemented by the backend
/// (<c>ProtectedClaudeKeyStore</c>, T016). Callers check <see cref="StoredClaudeKey.StoreAvailable"/> first (503 otherwise) and
/// call <see cref="IClaudeKeyProvider.Invalidate"/> afterwards.
/// </summary>
public interface IStoredClaudeKeyWriter
{
    /// <summary>Encrypts and saves the key, replacing a stored one in the same transaction.</summary>
    /// <param name="key">A well-formed key.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when an existing stored key was replaced.</returns>
    Task<bool> StoreAsync(ClaudeApiKey key, CancellationToken cancellationToken);

    /// <summary>Removes the stored key, readable or not; idempotent.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task DeleteAsync(CancellationToken cancellationToken);
}

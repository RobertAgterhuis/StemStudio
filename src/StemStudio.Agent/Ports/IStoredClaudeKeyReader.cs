namespace StemStudio.Agent.Ports;

/// <summary>
/// Read side of the stored Claude key (data-model 4). Implemented by the backend (<c>ProtectedClaudeKeyStore</c> in
/// <c>StemStudio.Api/Agent/Secrets</c>, T016) with ASP.NET Core Data Protection; <c>StemStudio.Agent</c> never references
/// Data Protection or EF.
/// </summary>
public interface IStoredClaudeKeyReader
{
    /// <summary>
    /// Reads and decrypts the stored key. A payload that cannot be decrypted (other or missing key ring, corrupted, revoked)
    /// is reported as unreadable, never thrown; the exception text and the ciphertext are never logged.
    /// </summary>
    /// <returns>What is stored and whether the store is usable.</returns>
    StoredClaudeKey Read();
}

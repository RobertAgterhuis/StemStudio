using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace StemStudio.Api.Agent.Secrets;

/// <summary>Result of <see cref="ClaudeKeyProtector.Unprotect"/>: the plaintext, or the type name of the failure.</summary>
/// <param name="Plaintext">The decrypted value; null on failure.</param>
/// <param name="RequiresMigration">True when the payload was made with a key that is no longer the default (re-protect it).</param>
/// <param name="FailureType">Exception type name on failure (never the message); else null.</param>
public sealed record UnprotectResult(string? Plaintext, bool RequiresMigration, string? FailureType);

/// <summary>
/// The Data Protection protector of the stored Claude key (purpose <see cref="DataProtectionSetup.ClaudeKeyPurpose"/>) and the
/// state of the key ring (011 follow-up claude-key, research R-01). Ciphertexts are base64url Data Protection payloads.
/// </summary>
public sealed class ClaudeKeyProtector
{
    private readonly IDataProtector _protector;
    private readonly KeyRingStatus _keyRing;

    /// <summary>Creates the protector.</summary>
    /// <param name="provider">Data Protection provider of the API.</param>
    /// <param name="keyRing">Outcome of the key ring directory check.</param>
    public ClaudeKeyProtector(IDataProtectionProvider provider, KeyRingStatus keyRing)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(keyRing);
        _protector = provider.CreateProtector(DataProtectionSetup.ClaudeKeyPurpose);
        _keyRing = keyRing;
    }

    /// <summary>False when the key ring directory is unusable (an ephemeral provider is in use; nothing may be stored).</summary>
    public bool Available => _keyRing.Available;

    /// <summary>Why the key ring is unusable; null when available.</summary>
    public string? UnavailableReason => _keyRing.UnavailableReason;

    /// <summary>Encrypts a value.</summary>
    /// <param name="plaintext">The value.</param>
    /// <returns>The base64url payload.</returns>
    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return WebEncoders.Base64UrlEncode(_protector.Protect(Encoding.UTF8.GetBytes(plaintext)));
    }

    /// <summary>Decrypts a payload; never throws for a bad, foreign or revoked payload.</summary>
    /// <param name="ciphertext">The base64url payload.</param>
    /// <returns>The plaintext and whether it needs re-protection, or the failure type.</returns>
    public UnprotectResult Unprotect(string ciphertext)
    {
        try
        {
            var payload = WebEncoders.Base64UrlDecode(ciphertext);
            var (bytes, requiresMigration) = Decrypt(payload);
            return new UnprotectResult(Encoding.UTF8.GetString(bytes), requiresMigration, null);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            return new UnprotectResult(null, false, exception.GetType().Name);
        }
    }

    private (byte[] Bytes, bool RequiresMigration) Decrypt(byte[] payload)
    {
        if (_protector is IPersistedDataProtector persisted)
        {
            var bytes = persisted.DangerousUnprotect(payload, ignoreRevocationErrors: false, out var requiresMigration, out _);
            return (bytes, requiresMigration);
        }

        return (_protector.Unprotect(payload), false);
    }
}

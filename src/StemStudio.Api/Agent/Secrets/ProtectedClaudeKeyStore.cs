using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;
using StemStudio.Persistence.Agent;

namespace StemStudio.Api.Agent.Secrets;

/// <summary>
/// The stored Claude key (011 follow-up claude-key, data-model 4, CK-FR-002, CK-FR-011): encrypts with Data Protection before
/// writing, decrypts on read, maps every decrypt failure to <see cref="StoredClaudeKeyStateDto.Unreadable"/> and re-protects a
/// payload made with an older key. Only the ciphertext, the last four characters and the time reach the database; logs carry
/// neither the key, nor its last four, nor the ciphertext, nor an exception message.
/// </summary>
public sealed partial class ProtectedClaudeKeyStore : IStoredClaudeKeyReader, IStoredClaudeKeyWriter
{
    /// <summary>Version of the protector purpose written with each row.</summary>
    public const int PurposeVersion = 1;

    private const string NotAKeyFailure = "InvalidKeyFormat";

    private readonly ClaudeKeyProtector _protector;
    private readonly ClaudeKeyRow _row;
    private readonly TimeProvider _clock;
    private readonly ILogger<ProtectedClaudeKeyStore> _logger;
    private int _unreadableLogged;

    /// <summary>Creates the store.</summary>
    /// <param name="protector">The key protector and key ring state.</param>
    /// <param name="row">Access to the secret row.</param>
    /// <param name="clock">Clock of the save time.</param>
    /// <param name="logger">Logger.</param>
    public ProtectedClaudeKeyStore(ClaudeKeyProtector protector, ClaudeKeyRow row, TimeProvider clock, ILogger<ProtectedClaudeKeyStore> logger)
    {
        _protector = protector;
        _row = row;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public StoredClaudeKey Read()
    {
        var row = _row.Find();
        if (row is null)
        {
            return StoredClaudeKey.Absent with { StoreAvailable = _protector.Available, StoreUnavailableReason = _protector.UnavailableReason };
        }

        var result = _protector.Unprotect(row.Ciphertext);
        if (Parse(result) is not { } key)
        {
            LogUnreadableOnce(result.FailureType ?? NotAKeyFailure);
            return Found(row, StoredClaudeKeyStateDto.Unreadable, null, row.StoredAt);
        }

        var storedAt = result.RequiresMigration ? Reprotect(key, row.StoredAt) : row.StoredAt;
        return Found(row, StoredClaudeKeyStateDto.Readable, key, storedAt);
    }

    private static ClaudeApiKey? Parse(UnprotectResult result) =>
        result.Plaintext is { } plaintext ? ClaudeApiKey.Parse(plaintext).Key : null;

    /// <inheritdoc />
    public async Task<bool> StoreAsync(ClaudeApiKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!_protector.Available)
        {
            throw new InvalidOperationException("The key store is not available; check StoreAvailable before storing.");
        }

        var ciphertext = _protector.Protect(key.Reveal());
        var replaced = await _row.UpsertAsync(ciphertext, key.LastFour, _clock.GetUtcNow().UtcDateTime, cancellationToken);
        LogStored(_logger, replaced);
        return replaced;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (await _row.DeleteAsync(cancellationToken))
        {
            LogDeleted(_logger);
        }
    }

    private DateTime Reprotect(ClaudeApiKey key, DateTime storedAt)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        return _row.Reprotect(_protector.Protect(key.Reveal()), now) ? now : storedAt;
    }

    private StoredClaudeKey Found(AgentSecretEntity row, StoredClaudeKeyStateDto state, ClaudeApiKey? key, DateTime storedAt) => new()
    {
        State = state,
        Key = key,
        LastFour = row.LastFour,
        StoredAt = new DateTimeOffset(DateTime.SpecifyKind(storedAt, DateTimeKind.Utc)),
        StoreAvailable = _protector.Available,
        StoreUnavailableReason = _protector.UnavailableReason,
    };

    private void LogUnreadableOnce(string failureType)
    {
        if (Interlocked.Exchange(ref _unreadableLogged, 1) == 0)
        {
            LogUnreadable(_logger, failureType);
        }
    }

    [LoggerMessage(EventId = 4100, EventName = "ClaudeKeyStored", Level = LogLevel.Information,
        Message = "Claude key stored (source: stored, lastFourPresent: True, replaced: {Replaced})")]
    private static partial void LogStored(ILogger logger, bool replaced);

    [LoggerMessage(EventId = 4101, EventName = "ClaudeKeyUnreadable", Level = LogLevel.Warning,
        Message = "The stored Claude key cannot be decrypted ({FailureType}); Claude needs the key to be entered again")]
    private static partial void LogUnreadable(ILogger logger, string failureType);

    [LoggerMessage(EventId = 4102, EventName = "ClaudeKeyDeleted", Level = LogLevel.Information, Message = "Stored Claude key deleted")]
    private static partial void LogDeleted(ILogger logger);
}

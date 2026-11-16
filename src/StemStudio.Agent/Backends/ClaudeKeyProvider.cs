using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Backends;

/// <summary>
/// <see cref="IClaudeKeyProvider"/> (011 follow-up claude-key, data-model 3/4, CK-FR-001, CK-FR-008, CK-FR-011): the
/// <c>ANTHROPIC_API_KEY</c> environment variable wins, else the readable stored key. The environment value is trimmed and
/// format-checked like a stored key (whitespace counts as unset; a malformed value is not used and the status says why).
/// The resolution is cached until <see cref="Invalidate"/> or a change of the environment value; the stored key is read only
/// when the cache is rebuilt. Never logs, prints or returns a key except through <see cref="ClaudeApiKey"/>.
/// </summary>
public sealed class ClaudeKeyProvider : IClaudeKeyProvider
{
    /// <summary>The environment variable holding the key.</summary>
    public const string KeyVariable = "ANTHROPIC_API_KEY";

    /// <summary>Reason of an unreadable stored key (contract example of <c>ClaudeKeyStatus.reason</c>).</summary>
    public const string UnreadableReason =
        "The stored key cannot be read on this installation (the encryption keys are missing or different, e.g. after a restore on another computer).";

    /// <summary>Fix of an unreadable stored key.</summary>
    public const string UnreadableFix = "Enter the key again.";

    /// <summary>Fix of a malformed environment value.</summary>
    public const string MalformedEnvironmentFix = "Correct or unset ANTHROPIC_API_KEY in the shell that starts StemStudio, then restart it.";

    /// <summary>Fix of an unavailable key store.</summary>
    public const string StoreUnavailableFix =
        "Set StemStudio__DataProtection__KeyRingDir to a writable directory outside the library, the backups, the application and any git repository, then restart StemStudio.";

    private readonly IEnvironmentReader _environment;
    private readonly IStoredClaudeKeyReader _stored;
    private readonly Lock _gate = new();
    private Snapshot? _cached;

    /// <summary>Creates the provider.</summary>
    /// <param name="environment">Environment reader (source of <c>ANTHROPIC_API_KEY</c>).</param>
    /// <param name="stored">Read side of the stored key.</param>
    public ClaudeKeyProvider(IEnvironmentReader environment, IStoredClaudeKeyReader stored)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(stored);
        _environment = environment;
        _stored = stored;
    }

    /// <summary>Reason of a malformed environment value; names the failed rule, never the value.</summary>
    /// <param name="code">A code of <see cref="ClaudeKeyInvalidReasons"/>.</param>
    /// <returns>The text.</returns>
    public static string MalformedEnvironmentReason(string code) =>
        $"ANTHROPIC_API_KEY is set but is not a usable Claude key (rule: {code}), so it is not used.";

    /// <summary>Reason of an unavailable key store, in words (data-model 5); never a path or stack trace.</summary>
    /// <param name="code">A code of <see cref="KeyStoreUnavailableReasons"/>, or null.</param>
    /// <returns>The text.</returns>
    public static string StoreUnavailableReason(string? code) =>
        $"The key store is not available: the key ring directory {Words(code)}. The ANTHROPIC_API_KEY environment variable still works.";

    /// <inheritdoc />
    public ClaudeKeyResolution Resolve() => Current().Resolution;

    /// <inheritdoc />
    public SelectedClaudeKey? KeyFor(ClaudeKeyTestSourceDto source)
    {
        var snapshot = Current();
        return source switch
        {
            ClaudeKeyTestSourceDto.Active => snapshot.Resolution is { Key: { } key, Source: { } from } ? new SelectedClaudeKey(key, from) : null,
            ClaudeKeyTestSourceDto.Environment => snapshot.Environment is { } environment ? new SelectedClaudeKey(environment, ClaudeKeySourceDto.Environment) : null,
            ClaudeKeyTestSourceDto.Stored => snapshot.Stored.Key is { } stored ? new SelectedClaudeKey(stored, ClaudeKeySourceDto.Stored) : null,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown key source."),
        };
    }

    /// <inheritdoc />
    public void Invalidate()
    {
        lock (_gate)
        {
            _cached = null;
        }
    }

    private Snapshot Current()
    {
        var raw = _environment.GetVariable(KeyVariable)?.Trim() ?? string.Empty;
        lock (_gate)
        {
            if (_cached is not { } cached || !string.Equals(cached.RawEnvironment, raw, StringComparison.Ordinal))
            {
                _cached = cached = Build(raw, _stored.Read());
            }

            return cached;
        }
    }

    private static Snapshot Build(string raw, StoredClaudeKey stored)
    {
        var parsed = raw.Length == 0 ? null : ClaudeApiKey.Parse(raw);
        var environment = parsed?.Key;
        var storedKey = stored.State == StoredClaudeKeyStateDto.Readable ? stored.Key : null;
        var (reason, fix) = Explanation(stored, parsed?.Error);
        var state = State(environment, storedKey, stored.State);
        var resolution = Resolution(Active(environment, storedKey), state, stored) with { Reason = reason, Fix = fix };
        return new Snapshot(raw, resolution, environment, stored with { Key = storedKey });
    }

    private static ClaudeKeyResolution Resolution((ClaudeApiKey? Key, ClaudeKeySourceDto? Source) active, ClaudeKeyStateDto state, StoredClaudeKey stored) => new()
    {
        Key = active.Key,
        Source = active.Source,
        State = state,
        LastFour = active.Key?.LastFour,
        Stored = stored.State,
        StoredLastFour = stored.LastFour,
        StoredAt = stored.StoredAt,
        StoreAvailable = stored.StoreAvailable,
        StoreUnavailableReason = stored.StoreUnavailableReason,
    };

    private static (ClaudeApiKey? Key, ClaudeKeySourceDto? Source) Active(ClaudeApiKey? environment, ClaudeApiKey? stored) =>
        environment is not null ? (environment, ClaudeKeySourceDto.Environment) : (stored, stored is null ? null : ClaudeKeySourceDto.Stored);

    private static ClaudeKeyStateDto State(ClaudeApiKey? environment, ClaudeApiKey? stored, StoredClaudeKeyStateDto storedState) =>
        (environment, stored, storedState) switch
        {
            ({ }, _, _) => ClaudeKeyStateDto.Environment,
            (null, { }, _) => ClaudeKeyStateDto.Stored,
            (null, null, StoredClaudeKeyStateDto.Unreadable) => ClaudeKeyStateDto.StoredUnreadable,
            _ => ClaudeKeyStateDto.NotConfigured,
        };

    /// <summary>One reason and fix, most actionable first: unusable store, unreadable stored key, malformed environment value.</summary>
    private static (string? Reason, string? Fix) Explanation(StoredClaudeKey stored, string? environmentError)
    {
        if (!stored.StoreAvailable)
        {
            return (StoreUnavailableReason(stored.StoreUnavailableReason), StoreUnavailableFix);
        }

        if (stored.State == StoredClaudeKeyStateDto.Unreadable)
        {
            return (UnreadableReason, UnreadableFix);
        }

        return environmentError is null ? (null, null) : (MalformedEnvironmentReason(environmentError), MalformedEnvironmentFix);
    }

    private static string Words(string? code) => code switch
    {
        KeyStoreUnavailableReasons.NotWritable => "is missing or not writable",
        KeyStoreUnavailableReasons.InsideLibrary => "is inside the library folder",
        KeyStoreUnavailableReasons.InsideBackups => "is inside the backups folder",
        KeyStoreUnavailableReasons.InsideApp => "is inside the application folder",
        KeyStoreUnavailableReasons.InsideRepository => "is inside a git repository",
        _ => "cannot be used",
    };

    /// <summary>A class, not a record: no generated <c>ToString</c> may print the raw environment value.</summary>
    private sealed class Snapshot(string rawEnvironment, ClaudeKeyResolution resolution, ClaudeApiKey? environment, StoredClaudeKey stored)
    {
        public string RawEnvironment { get; } = rawEnvironment;

        public ClaudeKeyResolution Resolution { get; } = resolution;

        public ClaudeApiKey? Environment { get; } = environment;

        public StoredClaudeKey Stored { get; } = stored;
    }
}

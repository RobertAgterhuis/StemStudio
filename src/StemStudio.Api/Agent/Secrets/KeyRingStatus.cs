namespace StemStudio.Api.Agent.Secrets;

/// <summary>
/// Outcome of the key ring directory check at start (011 follow-up claude-key, CK-FR-003/005). When unavailable the API runs
/// with an ephemeral Data Protection provider, refuses to store a key (503 <c>key-store-unavailable</c>) and reports
/// <c>storeAvailable: false</c>.
/// </summary>
public sealed partial class KeyRingStatus
{
    private int _logged;

    private KeyRingStatus(string path, string? unavailableReason, UnixFileMode? wideMode)
    {
        Path = path;
        UnavailableReason = unavailableReason;
        WideMode = wideMode;
    }

    /// <summary>The resolved key ring directory.</summary>
    public string Path { get; }

    /// <summary>True when the ring is persisted in <see cref="Path"/>.</summary>
    public bool Available => UnavailableReason is null;

    /// <summary>A <c>KeyStoreUnavailableReasons</c> code; null when available.</summary>
    public string? UnavailableReason { get; }

    /// <summary>The directory's mode when it is wider than 0700 and could not be tightened; else null.</summary>
    public UnixFileMode? WideMode { get; }

    /// <summary>A usable directory.</summary>
    /// <param name="path">Directory.</param>
    /// <param name="wideMode">Mode that could not be tightened, if any.</param>
    /// <returns>The status.</returns>
    public static KeyRingStatus Ready(string path, UnixFileMode? wideMode) => new(path, null, wideMode);

    /// <summary>An unusable directory.</summary>
    /// <param name="path">Directory.</param>
    /// <param name="reason">Reason code.</param>
    /// <returns>The status.</returns>
    public static KeyRingStatus Unavailable(string path, string reason) => new(path, reason, null);

    /// <summary>Logs <c>KeyStoreUnavailable</c> (4104) or <c>KeyRingPermissionsWide</c> (4103), once per status.</summary>
    /// <param name="logger">Logger.</param>
    public void LogStartupWarnings(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (Interlocked.Exchange(ref _logged, 1) == 1)
        {
            return;
        }

        if (UnavailableReason is not null)
        {
            LogKeyStoreUnavailable(logger, UnavailableReason);
        }
        else if (WideMode is { } mode)
        {
            LogPermissionsWide(logger, Path, mode);
        }
    }

    [LoggerMessage(EventId = 4104, EventName = "KeyStoreUnavailable", Level = LogLevel.Warning,
        Message = "The Claude key store is not available ({Reason}); storing a key is refused, ANTHROPIC_API_KEY still works")]
    private static partial void LogKeyStoreUnavailable(ILogger logger, string reason);

    [LoggerMessage(EventId = 4103, EventName = "KeyRingPermissionsWide", Level = LogLevel.Warning,
        Message = "The key ring directory {Directory} is accessible to others (mode {Mode}) and could not be restricted to the app user")]
    private static partial void LogPermissionsWide(ILogger logger, string directory, UnixFileMode mode);
}

namespace StemStudio.Agent.Runtime;

/// <summary>
/// The selected backend cannot serve a turn (agent off, backend not configured, unreachable). The runner ends the run with
/// <c>backend-unavailable</c> and this readable message; the message must not contain secrets.
/// </summary>
public sealed class BackendUnavailableException : Exception
{
    /// <summary>Creates the exception with a default message.</summary>
    public BackendUnavailableException()
        : this("No language model is available for this run.")
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">Readable message.</param>
    public BackendUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with its cause (logged, never shown).</summary>
    /// <param name="message">Readable message.</param>
    /// <param name="innerException">Cause.</param>
    public BackendUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

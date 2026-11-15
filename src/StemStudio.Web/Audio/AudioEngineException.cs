namespace StemStudio.Web.Audio;

/// <summary>The audio engine rejected a call (e.g. a stem failed to load or the loop region is invalid).</summary>
public sealed class AudioEngineException : Exception
{
    /// <summary>Creates an exception with a generic message.</summary>
    public AudioEngineException()
        : this("The audio engine reported an error.")
    {
    }

    /// <summary>Creates an exception with the engine's message.</summary>
    /// <param name="message">The readable message.</param>
    public AudioEngineException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with the engine's message and the original <c>JSException</c>.</summary>
    /// <param name="message">The readable message.</param>
    /// <param name="innerException">The original exception.</param>
    public AudioEngineException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

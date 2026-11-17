namespace StemStudio.Domain.Recording;

/// <summary>Why an uploaded WAV is refused (data-model 2.3).</summary>
public enum TakeWavError
{
    /// <summary>400 <c>invalid-wav</c>: not a readable RIFF/WAVE file (also truncated or inconsistent).</summary>
    Invalid,

    /// <summary>415 <c>unsupported-wav-format</c>: a readable WAV in another format; <see cref="TakeWavException.Field"/> names the field.</summary>
    Unsupported,

    /// <summary>413: the audio data is larger than the upload limit.</summary>
    TooLarge,
}

/// <summary>Thrown by <see cref="TakeWavReader"/> and <see cref="TakeWavPadder"/> for a refused WAV.</summary>
public sealed class TakeWavException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="error">The kind of refusal.</param>
    /// <param name="message">A readable reason.</param>
    /// <param name="field">The format field of an unsupported WAV, or null.</param>
    public TakeWavException(TakeWavError error, string message, string? field = null)
        : base(message)
    {
        Error = error;
        Field = field;
    }

    /// <summary>Creates an <see cref="TakeWavError.Invalid"/> exception.</summary>
    public TakeWavException()
        : this(TakeWavError.Invalid, "The file is not a readable WAV file.")
    {
    }

    /// <summary>Creates an <see cref="TakeWavError.Invalid"/> exception.</summary>
    /// <param name="message">A readable reason.</param>
    public TakeWavException(string message)
        : this(TakeWavError.Invalid, message)
    {
    }

    /// <summary>Creates an <see cref="TakeWavError.Invalid"/> exception.</summary>
    /// <param name="message">A readable reason.</param>
    /// <param name="innerException">The cause.</param>
    public TakeWavException(string message, Exception innerException)
        : base(message, innerException)
    {
        Error = TakeWavError.Invalid;
    }

    /// <summary>The kind of refusal.</summary>
    public TakeWavError Error { get; }

    /// <summary>The format field of an unsupported WAV (<c>audioFormat</c>, <c>channels</c>, <c>sampleRate</c>, <c>bitsPerSample</c>), or null.</summary>
    public string? Field { get; }
}

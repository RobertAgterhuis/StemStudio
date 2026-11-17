namespace StemStudio.Web.Recording;

/// <summary>Machine-readable prefix of a recording error message (audio-engine 2.5.0 "AudioEngine methods").</summary>
public enum RecordingErrorCode
{
    /// <summary>No known prefix.</summary>
    Other,

    /// <summary><c>permissionDenied:</c> the user or the browser refused the microphone.</summary>
    PermissionDenied,

    /// <summary><c>notFound:</c> no input device.</summary>
    NotFound,

    /// <summary><c>busy:</c> the device is used by another program.</summary>
    Busy,

    /// <summary><c>modeUnavailable:</c> mono-2/stereo on a one-channel device.</summary>
    ModeUnavailable,

    /// <summary><c>unsupported:</c> the browser cannot record here.</summary>
    Unsupported,

    /// <summary><c>recordingSpeed:</c> speed or transpose not neutral.</summary>
    RecordingSpeed,

    /// <summary><c>speedTrainer:</c> the speed trainer is on.</summary>
    SpeedTrainer,
}

/// <summary>A recording call the engine rejected; <see cref="Exception.Message"/> is the readable sentence without the code.</summary>
public sealed class RecordingException : Exception
{
    private static readonly (string Prefix, RecordingErrorCode Code)[] Prefixes =
    [
        ("permissionDenied:", RecordingErrorCode.PermissionDenied),
        ("notFound:", RecordingErrorCode.NotFound),
        ("busy:", RecordingErrorCode.Busy),
        ("modeUnavailable:", RecordingErrorCode.ModeUnavailable),
        ("unsupported:", RecordingErrorCode.Unsupported),
        ("recordingSpeed:", RecordingErrorCode.RecordingSpeed),
        ("speedTrainer:", RecordingErrorCode.SpeedTrainer),
    ];

    /// <summary>Creates an exception with <see cref="RecordingErrorCode.Other"/>.</summary>
    public RecordingException()
        : this("The recording failed.")
    {
    }

    /// <summary>Creates an exception with <see cref="RecordingErrorCode.Other"/>.</summary>
    /// <param name="message">Readable message.</param>
    public RecordingException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with <see cref="RecordingErrorCode.Other"/>.</summary>
    /// <param name="message">Readable message.</param>
    /// <param name="innerException">The cause.</param>
    public RecordingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    private RecordingException(RecordingErrorCode code, string message, Exception innerException)
        : base(message, innerException) => Code = code;

    /// <summary>The parsed code.</summary>
    public RecordingErrorCode Code { get; }

    /// <summary>Parses an engine message (<c>code: sentence</c>; Blazor appends the JS stack after a line break).</summary>
    /// <param name="engineMessage">The JS error message.</param>
    /// <param name="innerException">The JS exception.</param>
    /// <returns>The exception with code and sentence.</returns>
    public static RecordingException FromEngine(string engineMessage, Exception innerException)
    {
        var line = engineMessage.Split('\n')[0].Trim();
        foreach (var (prefix, code) in Prefixes)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return new RecordingException(code, line[prefix.Length..].Trim(), innerException);
            }
        }

        return new RecordingException(RecordingErrorCode.Other, line, innerException);
    }
}

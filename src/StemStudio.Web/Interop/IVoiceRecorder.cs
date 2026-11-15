using Microsoft.AspNetCore.Components;

namespace StemStudio.Web.Interop;

/// <summary>
/// C# side of the voice recorder in <c>web-interop.js</c> (feature 010, FR-005): microphone recording with MediaRecorder
/// and preview URLs of chosen files. Faked in unit tests. The recording only travels from the page to this server and on
/// to the local StemStudio API (FR-015).
/// </summary>
public interface IVoiceRecorder
{
    /// <summary><c>recorderUnavailableReason()</c>: why the page cannot record, or null.</summary>
    /// <returns>The readable reason; upload stays possible.</returns>
    ValueTask<string?> UnavailableReasonAsync();

    /// <summary><c>startRecording(maxSeconds)</c>: asks for the microphone and starts recording.</summary>
    /// <param name="maxSeconds">The recording stops by itself after this many seconds.</param>
    /// <returns>The running recording.</returns>
    /// <exception cref="VoiceRecorderException">The microphone cannot be used (permission denied, none found).</exception>
    ValueTask<IVoiceRecording> StartAsync(int maxSeconds);

    /// <summary><c>fileUrl(input)</c>: a preview URL of the file chosen in a file input.</summary>
    /// <param name="input">The input element.</param>
    /// <returns>An object URL, or null.</returns>
    ValueTask<string?> FileUrlAsync(ElementReference input);

    /// <summary><c>revokeUrl(url)</c>: releases a preview URL.</summary>
    /// <param name="url">The object URL.</param>
    /// <returns>A task.</returns>
    ValueTask RevokeUrlAsync(string url);
}

/// <summary>A running recording; disposing it stops the microphone and releases its preview.</summary>
public interface IVoiceRecording : IAsyncDisposable
{
    /// <summary>Stops the recording (if still running) and reads it.</summary>
    /// <returns>The recorded sample.</returns>
    /// <exception cref="VoiceRecorderException">The recording could not be read.</exception>
    ValueTask<RecordedSample> StopAsync();
}

/// <summary>A recorded sample.</summary>
/// <param name="Bytes">The audio (WebM/Opus where the browser supports it).</param>
/// <param name="ContentType">Its MIME type.</param>
/// <param name="PreviewUrl">Object URL for the <c>voice-preview</c> player, or null.</param>
public sealed record RecordedSample(byte[] Bytes, string ContentType, string? PreviewUrl);

/// <summary>The microphone or the recording cannot be used; the message is readable.</summary>
public sealed class VoiceRecorderException : Exception
{
    /// <summary>Creates the exception.</summary>
    public VoiceRecorderException()
        : this("The recording failed.")
    {
    }

    /// <summary>Creates the exception with a readable message.</summary>
    /// <param name="message">The message.</param>
    public VoiceRecorderException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a readable message and its cause.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public VoiceRecorderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

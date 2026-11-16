using StemStudio.Contracts.Mixes;

namespace StemStudio.Web.Mixer;

/// <summary>Stub.</summary>
/// <param name="time">T.</param>
/// <param name="send">S.</param>
public sealed class PracticeSender(TimeProvider time, Func<PracticeSettingsDto, Task> send) : IDisposable
{
    /// <summary>Stub.</summary>
    public static TimeSpan Interval { get; } = TimeSpan.FromMilliseconds(50);

    /// <summary>Stub.</summary>
    public PracticeSettingsDto Sent => throw new NotImplementedException();

    /// <summary>Stub.</summary>
    /// <param name="settings">S.</param>
    /// <returns>R.</returns>
    public Task SubmitAsync(PracticeSettingsDto settings) => throw new NotImplementedException(time.ToString() + send.ToString() + settings);

    /// <inheritdoc />
    public void Dispose()
    {
    }
}

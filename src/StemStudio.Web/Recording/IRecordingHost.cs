using StemStudio.Contracts.Mixes;

namespace StemStudio.Web.Recording;

/// <summary>What the recording UI needs from the mixer page (implemented by <c>Mixer.Recording.cs</c>).</summary>
public interface IRecordingHost
{
    /// <summary>The open track.</summary>
    Guid TrackId { get; }

    /// <summary>The open mix, or null.</summary>
    Guid? MixId { get; }

    /// <summary>True once the track is loaded into the engine.</summary>
    bool TrackReady { get; }

    /// <summary>The current practice settings (speed, transpose, trainer).</summary>
    PracticeSettingsDto Practice { get; }

    /// <summary>Sets speed 100 % and transpose 0 (the "Set to 100 %" action, FR-009).</summary>
    /// <returns>A task that completes when applied.</returns>
    Task SetNormalSpeedAsync();

    /// <summary>Moves the playhead.</summary>
    /// <param name="seconds">The position.</param>
    /// <returns>A task that completes when applied.</returns>
    Task SeekAsync(double seconds);

    /// <summary>Re-renders the mixer on its renderer thread.</summary>
    /// <returns>A task that completes when queued.</returns>
    Task RefreshAsync();
}

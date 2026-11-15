using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Experience;

/// <summary>Where a Beginner is on the journey Import -> Separate -> Mix -> Save (FR-015).</summary>
public enum JourneyStage
{
    /// <summary>On the import screen.</summary>
    Import,

    /// <summary>Waiting for (or retrying) the separation.</summary>
    Separate,

    /// <summary>Practising in the mixer, or ready to open it.</summary>
    Mix,

    /// <summary>The mixer shows a saved mix without unsaved changes: every step is done.</summary>
    Saved,
}

/// <summary>Maps screen state to the journey stage.</summary>
public static class JourneyStages
{
    /// <summary>Track page: completed tracks are ready to mix, everything else is still separating.</summary>
    /// <param name="status">The track status.</param>
    /// <returns>The stage.</returns>
    public static JourneyStage ForTrack(TrackStatusDto status) =>
        status == TrackStatusDto.Completed ? JourneyStage.Mix : JourneyStage.Separate;

    /// <summary>Mixer: Saved only for a saved mix without unsaved changes.</summary>
    /// <param name="saved">True when the mixer has a mix id.</param>
    /// <param name="dirty">True with unsaved changes.</param>
    /// <returns>The stage.</returns>
    public static JourneyStage ForMixer(bool saved, bool dirty) => saved && !dirty ? JourneyStage.Saved : JourneyStage.Mix;
}

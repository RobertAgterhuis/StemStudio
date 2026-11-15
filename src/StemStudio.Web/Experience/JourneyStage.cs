using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Experience;

/// <summary>Stage. Red stub (T041).</summary>
public enum JourneyStage
{
    /// <summary>Import.</summary>
    Import,

    /// <summary>Separate.</summary>
    Separate,

    /// <summary>Mix.</summary>
    Mix,

    /// <summary>Saved.</summary>
    Saved,
}

/// <summary>Red stub (T041).</summary>
public static class JourneyStages
{
    /// <summary>Stub.</summary>
    /// <param name="status">Status.</param>
    /// <returns>Stage.</returns>
    public static JourneyStage ForTrack(TrackStatusDto status) => status == TrackStatusDto.Completed ? JourneyStage.Import : JourneyStage.Import;

    /// <summary>Stub.</summary>
    /// <param name="saved">Saved.</param>
    /// <param name="dirty">Dirty.</param>
    /// <returns>Stage.</returns>
    public static JourneyStage ForMixer(bool saved, bool dirty) => saved && dirty ? JourneyStage.Import : JourneyStage.Import;
}

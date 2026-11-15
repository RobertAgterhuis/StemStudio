namespace StemStudio.Web.Experience;

/// <summary>
/// Experience level of the user (data-model.md 2). Ordered: a higher value sees at least the
/// controls of a lower one, except Beginner-only aids. Stored lower-case in preferences.
/// </summary>
public enum ExperienceLevel
{
    /// <summary>Guided: simplified controls, journey rail, presets, inline hints.</summary>
    Beginner = 0,

    /// <summary>Today's full control set (every 1.2.0 UI test id).</summary>
    Advanced = 1,

    /// <summary>Advanced plus numeric entry, dB readouts, shortcuts overlay and job details.</summary>
    Expert = 2,
}

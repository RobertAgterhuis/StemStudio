namespace StemStudio.Web.Preferences;

/// <summary>Stored motion choice (data-model.md 1.1 <c>motion</c>).</summary>
public enum MotionPreference
{
    /// <summary>Follow the OS <c>prefers-reduced-motion</c> setting. Default.</summary>
    System = 0,

    /// <summary>Always reduced motion.</summary>
    Reduced = 1,
}

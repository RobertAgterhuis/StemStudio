namespace StemStudio.Web.Practice;

/// <summary>Layer of a chord: normal keys or the foot-pedal layer (web-interop 1.1.0 <c>KeyBinding.layer</c>).</summary>
public enum KeyLayer
{
    /// <summary>Normal keyboard shortcuts.</summary>
    Keys = 0,

    /// <summary>Foot-pedal chords; in pedal mode they also fire on non-text controls (FR-023).</summary>
    Pedal = 1,
}

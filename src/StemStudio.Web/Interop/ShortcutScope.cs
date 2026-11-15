namespace StemStudio.Web.Interop;

/// <summary>Key map scope of <see cref="IWebInterop.RegisterShortcutsAsync"/> (JS values <c>global</c>, <c>mixer</c>).</summary>
public enum ShortcutScope
{
    /// <summary>Keys valid on every screen.</summary>
    Global = 0,

    /// <summary>Mixer keys (loop, A/B, metronome, seek, channel selection, mute/solo, save).</summary>
    Mixer = 1,
}

namespace StemStudio.Web.Interop;

/// <summary>Key map scope of <see cref="IWebInterop.RegisterShortcutsAsync"/> (JS values <c>global</c>, <c>mixer</c>, <c>library</c>).</summary>
public enum ShortcutScope
{
    /// <summary>Keys valid on every screen.</summary>
    Global = 0,

    /// <summary>Mixer keys (loop, A/B, metronome, seek, channel selection, mute/solo, save).</summary>
    Mixer = 1,

    /// <summary>Library pages (feature 022): <c>/</c> focuses the library search (web-interop 1.2.0).</summary>
    Library = 2,
}

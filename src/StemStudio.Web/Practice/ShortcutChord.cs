namespace StemStudio.Web.Practice;

/// <summary>A chord of a user override (014 data-model 5.1 <c>shortcuts.overrides</c>).</summary>
/// <param name="Key"><c>KeyboardEvent.key</c> lower-cased, 1..20 characters (<c>" "</c> for Space).</param>
/// <param name="Mod">Ctrl (Cmd on macOS) required.</param>
/// <param name="Shift">Shift required (true) or forbidden (false); null: forbidden for letters, not checked otherwise.</param>
/// <param name="Layer">Keys or pedal layer.</param>
public sealed record ShortcutChord(string Key, bool Mod = false, bool? Shift = null, KeyLayer Layer = KeyLayer.Keys)
{
    /// <summary>The chord without its layer.</summary>
    public KeyChordData Chord => new(Key, Mod, Shift);
}

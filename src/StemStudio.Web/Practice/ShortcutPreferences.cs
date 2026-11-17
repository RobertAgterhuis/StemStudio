namespace StemStudio.Web.Practice;

/// <summary>Shortcut preferences (014 data-model 5.1, preferences schema version 2 <c>shortcuts</c>).</summary>
/// <param name="SingleKey">Single-key shortcuts on (FR-022; WCAG 2.2 SC 2.1.4).</param>
/// <param name="PedalMode">Pedal-layer chords also fire on non-text controls (FR-023).</param>
/// <param name="PedalPreset">Pedal preset id of the catalogue.</param>
/// <param name="Overrides">Command id to 0..2 chords; an empty list unbinds the command's keys.</param>
public sealed record ShortcutPreferences(
    bool SingleKey,
    bool PedalMode,
    string PedalPreset,
    IReadOnlyDictionary<string, IReadOnlyList<ShortcutChord>> Overrides)
{
    /// <summary>Single keys on, pedal mode off, preset <c>play-section-start</c>, no overrides.</summary>
    public static ShortcutPreferences Default { get; } =
        new(true, false, "play-section-start", new Dictionary<string, IReadOnlyList<ShortcutChord>>(StringComparer.Ordinal));
}

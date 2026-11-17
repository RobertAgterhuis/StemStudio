namespace StemStudio.Web.Practice;

/// <summary>The default key map catalogue (<c>keymap-defaults.json</c>, web-interop 1.1.0, feature 014 FR-020).</summary>
public sealed record KeyMapCatalogue
{
    /// <summary>Catalogue version (the web-interop contract version that defines it).</summary>
    public required string Version { get; init; }

    /// <summary>Overview groups in display order.</summary>
    public required IReadOnlyList<string> Groups { get; init; }

    /// <summary>Key map scopes (<c>global</c>, <c>mixer</c>, <c>setlist</c>, <c>library</c>).</summary>
    public required IReadOnlyList<string> Scopes { get; init; }

    /// <summary>Every command with its default chords.</summary>
    public required IReadOnlyList<KeyMapCommand> Commands { get; init; }

    /// <summary>Fixed two-key sequences (004 "G then X").</summary>
    public required IReadOnlyList<KeySequence> Sequences { get; init; }

    /// <summary>Pedal layer defaults and presets.</summary>
    public required PedalDefaults Pedal { get; init; }

    /// <summary>Chords that can never be bound (browser, focus and screen-reader keys).</summary>
    public required IReadOnlyList<ReservedChord> ReservedChords { get; init; }

    /// <summary>Chords handled outside the key map by another feature (never bindable).</summary>
    public required IReadOnlyList<ExternalChord> ExternalChords { get; init; }
}

/// <summary>A key chord: <c>KeyboardEvent.key</c> lower-cased, Mod (Ctrl, Cmd on macOS) and Shift.</summary>
/// <param name="Key">The key (<c>" "</c> for Space, <c>arrowleft</c>, <c>pagedown</c>, <c>f1</c>, a character).</param>
/// <param name="Mod">Ctrl (Cmd on macOS) required.</param>
/// <param name="Shift">Shift required (true) or forbidden (false); null = not checked.</param>
public sealed record KeyChordData(string Key, bool Mod = false, bool? Shift = null);

/// <summary>One command of the catalogue.</summary>
public sealed record KeyMapCommand
{
    /// <summary>Command id (data-model 5.2), passed to <c>OnCommand</c>.</summary>
    public required string Command { get; init; }

    /// <summary>Key map scope.</summary>
    public required string Scope { get; init; }

    /// <summary>Feature that provides the handler (<c>001</c>, <c>004</c>, <c>012</c>, <c>014</c>, <c>015</c>, <c>022</c>).</summary>
    public required string Owner { get; init; }

    /// <summary>Overview group.</summary>
    public required string Group { get; init; }

    /// <summary>Label in the overview and the settings.</summary>
    public required string Label { get; init; }

    /// <summary>Default chords (0..2).</summary>
    public required IReadOnlyList<KeyChordData> Defaults { get; init; }

    /// <summary>Held for a feature not shipped yet: never dispatched, shown or overridable; its chords stay taken.</summary>
    public bool Reserved { get; init; }

    /// <summary>Fires in typing contexts too (Mod chords only).</summary>
    public bool Anywhere { get; init; }
}

/// <summary>A fixed two-key sequence (not remappable).</summary>
/// <param name="Command">Command id.</param>
/// <param name="Scope">Scope (<c>global</c>).</param>
/// <param name="Owner">Owner feature.</param>
/// <param name="Label">Label.</param>
/// <param name="Keys">The two keys.</param>
public sealed record KeySequence(string Command, string Scope, string Owner, string Label, IReadOnlyList<string> Keys)
{
    /// <summary>Overview group.</summary>
    public string Group { get; init; } = "navigation";
}

/// <summary>Pedal layer defaults (FR-023).</summary>
/// <param name="DefaultPreset">Preset id used until the user picks one.</param>
/// <param name="LeftKey">Key the left pedal sends by default.</param>
/// <param name="RightKey">Key the right pedal sends by default.</param>
/// <param name="Presets">The presets.</param>
public sealed record PedalDefaults(string DefaultPreset, KeyChordData LeftKey, KeyChordData RightKey, IReadOnlyList<PedalPreset> Presets);

/// <summary>A two-pedal preset; null commands for <c>custom</c>.</summary>
/// <param name="Id">Preset id (<c>pedalPreset</c> in the preferences).</param>
/// <param name="Label">Label.</param>
/// <param name="Left">Command of the left pedal.</param>
/// <param name="Right">Command of the right pedal.</param>
public sealed record PedalPreset(string Id, string Label, string? Left, string? Right);

/// <summary>A chord that can never be bound.</summary>
/// <param name="Key">The key.</param>
/// <param name="Mod">Mod required.</param>
/// <param name="Shift">Shift required, forbidden or null (not checked).</param>
/// <param name="Reason">Reason shown when the user tries to assign it.</param>
public sealed record ReservedChord(string Key, bool Mod, bool? Shift, string Reason);

/// <summary>A chord another feature handles outside the key map (for example 023's lyrics editor).</summary>
/// <param name="Owner">Owner feature.</param>
/// <param name="Scope">Where it applies.</param>
/// <param name="Keys">Display form (<c>Ctrl+Alt+C</c>).</param>
/// <param name="Label">What it does.</param>
/// <param name="Chord">The chord when it is expressible without Alt; null for Alt chords (never bindable anyway).</param>
public sealed record ExternalChord(string Owner, string Scope, string Keys, string Label, KeyChordData? Chord);

using System.Text.Json.Serialization;

namespace StemStudio.Web.Practice;

/// <summary>A command with its chords in one layer (web-interop 1.1.0 <c>KeyBinding</c>), as passed to <c>registerShortcuts</c>.</summary>
/// <param name="Command">Command id (014 data-model 5.2).</param>
/// <param name="Chords">0..2 chords.</param>
/// <param name="Layer"><c>keys</c> or <c>pedal</c>.</param>
/// <param name="Owner">Feature that provides the handler.</param>
/// <param name="Anywhere">Fires in typing contexts too (Mod chords only).</param>
/// <param name="Reserved">Held for a feature not shipped yet: never dispatched.</param>
public sealed record KeyBinding(
    string Command,
    IReadOnlyList<KeyChordData> Chords,
    string Layer,
    string Owner,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Anywhere = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Reserved = false)
{
    /// <summary>JSON value of the keys layer.</summary>
    public const string KeysLayer = "keys";

    /// <summary>JSON value of the pedal layer.</summary>
    public const string PedalLayer = "pedal";
}

/// <summary>The key map of one scope (web-interop 1.1.0 <c>KeyMap</c>).</summary>
/// <param name="Bindings">The bindings.</param>
/// <param name="SingleKey">False: every chord without Mod is ignored.</param>
/// <param name="PedalMode">True: pedal-layer chords also fire on non-text controls.</param>
public sealed record KeyMap(IReadOnlyList<KeyBinding> Bindings, bool SingleKey, bool PedalMode);

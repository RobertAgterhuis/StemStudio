namespace StemStudio.Web.Practice;

/// <summary>A pedal of the two-pedal setup.</summary>
public enum PedalSide
{
    /// <summary>Left pedal (PageUp by default).</summary>
    Left = 0,

    /// <summary>Right pedal (PageDown by default).</summary>
    Right = 1,
}

/// <summary>
/// The pedal setup (FR-023, 014 data-model 5.1 presets). A preset maps the left and right pedal to two commands; a
/// learned pedal key is stored as a pedal-layer override chord of that command. The preset <c>custom</c> has no fixed
/// commands: its pedal chords are pedal-layer overrides edited per command (choosing it copies the previous preset).
/// </summary>
public static class PedalPresets
{
    /// <summary>Id of the preset without fixed commands.</summary>
    public const string Custom = "custom";

    /// <summary>The chosen preset (the catalogue default when the id is unknown).</summary>
    /// <param name="catalogue">The catalogue.</param>
    /// <param name="preferences">The shortcut preferences.</param>
    /// <returns>The preset.</returns>
    public static PedalPreset Current(KeyMapCatalogue catalogue, ShortcutPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(preferences);
        return catalogue.Pedal.Presets.FirstOrDefault(p => p.Id == preferences.PedalPreset)
            ?? catalogue.Pedal.Presets.First(p => p.Id == catalogue.Pedal.DefaultPreset);
    }

    /// <summary>The command of a pedal in the chosen preset (null for <c>custom</c>).</summary>
    /// <param name="catalogue">The catalogue.</param>
    /// <param name="preferences">The shortcut preferences.</param>
    /// <param name="side">The pedal.</param>
    /// <returns>The command id, or null.</returns>
    public static string? CommandOf(KeyMapCatalogue catalogue, ShortcutPreferences preferences, PedalSide side)
    {
        var preset = Current(catalogue, preferences);
        return side == PedalSide.Left ? preset.Left : preset.Right;
    }

    /// <summary>The key a pedal sends: the learned key, or the catalogue default (PageUp / PageDown).</summary>
    /// <param name="catalogue">The catalogue.</param>
    /// <param name="preferences">The shortcut preferences.</param>
    /// <param name="side">The pedal.</param>
    /// <returns>The chord.</returns>
    public static KeyChordData KeyOf(KeyMapCatalogue catalogue, ShortcutPreferences preferences, PedalSide side)
    {
        var fallback = side == PedalSide.Left ? catalogue.Pedal.LeftKey : catalogue.Pedal.RightKey;
        var command = CommandOf(catalogue, preferences, side);
        return command is not null && PedalChords(preferences, command) is [var learned, ..] ? learned.Chord : fallback;
    }

    /// <summary>Stores the key a pedal sends (Learn) on the preset's command; ignored for <c>custom</c>.</summary>
    /// <param name="catalogue">The catalogue.</param>
    /// <param name="preferences">The shortcut preferences.</param>
    /// <param name="side">The pedal.</param>
    /// <param name="chord">The captured chord.</param>
    /// <returns>The new preferences.</returns>
    public static ShortcutPreferences Learn(KeyMapCatalogue catalogue, ShortcutPreferences preferences, PedalSide side, KeyChordData chord)
    {
        ArgumentNullException.ThrowIfNull(chord);
        return CommandOf(catalogue, preferences, side) is { } command
            ? preferences with { Overrides = WithPedal(preferences.Overrides, command, chord) }
            : preferences;
    }

    /// <summary>Switches the preset; learned keys move to the new preset's commands, <c>custom</c> starts as a copy.</summary>
    /// <param name="catalogue">The catalogue.</param>
    /// <param name="preferences">The shortcut preferences.</param>
    /// <param name="presetId">The new preset id (unknown ids are ignored).</param>
    /// <returns>The new preferences.</returns>
    public static ShortcutPreferences Choose(KeyMapCatalogue catalogue, ShortcutPreferences preferences, string presetId)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        if (catalogue.Pedal.Presets.All(p => p.Id != presetId))
        {
            return preferences;
        }

        var keys = (Left: KeyOf(catalogue, preferences, PedalSide.Left), Right: KeyOf(catalogue, preferences, PedalSide.Right));
        var from = Current(catalogue, preferences);
        var to = catalogue.Pedal.Presets.First(p => p.Id == presetId);
        var overrides = WithoutPedal(preferences.Overrides);
        var (left, right) = to.Id == Custom ? (from.Left, from.Right) : (to.Left, to.Right);
        overrides = Assign(overrides, left, keys.Left, to.Id == Custom ? null : catalogue.Pedal.LeftKey);
        overrides = Assign(overrides, right, keys.Right, to.Id == Custom ? null : catalogue.Pedal.RightKey);
        return preferences with { PedalPreset = to.Id, Overrides = overrides };
    }

    /// <summary>The pedal-layer override chords of a command.</summary>
    /// <param name="preferences">The shortcut preferences.</param>
    /// <param name="command">The command id.</param>
    /// <returns>The chords (empty when none).</returns>
    public static IReadOnlyList<ShortcutChord> PedalChords(ShortcutPreferences preferences, string command)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        return preferences.Overrides.TryGetValue(command, out var chords) ? [.. chords.Where(c => c.Layer == KeyLayer.Pedal)] : [];
    }

    private static Dictionary<string, IReadOnlyList<ShortcutChord>> Assign(
        Dictionary<string, IReadOnlyList<ShortcutChord>> overrides, string? command, KeyChordData key, KeyChordData? implied) =>
        command is null || (implied is not null && KeyChords.Clash(key, implied)) ? overrides : WithPedal(overrides, command, key);

    private static Dictionary<string, IReadOnlyList<ShortcutChord>> WithPedal(
        IReadOnlyDictionary<string, IReadOnlyList<ShortcutChord>> overrides, string command, KeyChordData chord)
    {
        var result = new Dictionary<string, IReadOnlyList<ShortcutChord>>(overrides, StringComparer.Ordinal);
        var keys = result.TryGetValue(command, out var own) ? own.Where(c => c.Layer == KeyLayer.Keys).Take(1) : [];
        result[command] = [.. keys, new ShortcutChord(chord.Key, chord.Mod, chord.Shift, KeyLayer.Pedal)];
        return result;
    }

    private static Dictionary<string, IReadOnlyList<ShortcutChord>> WithoutPedal(IReadOnlyDictionary<string, IReadOnlyList<ShortcutChord>> overrides)
    {
        var result = new Dictionary<string, IReadOnlyList<ShortcutChord>>(StringComparer.Ordinal);
        foreach (var (command, chords) in overrides)
        {
            var keys = chords.Where(c => c.Layer == KeyLayer.Keys).ToList();
            if (keys.Count > 0 || chords.Count == 0)
            {
                result[command] = keys;
            }
        }

        return result;
    }
}

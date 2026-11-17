namespace StemStudio.Web.Practice;

/// <summary>
/// Builds the effective key map of a scope from the catalogue and the user's shortcut preferences (FR-019..FR-023):
/// keys layer = the override's keys-layer chords when the override is empty or has any (else the defaults); pedal layer
/// = the override's pedal-layer chords, else the preset's pedal key for the preset's two commands.
/// </summary>
public static class KeyMapBuilder
{
    /// <summary>Builds the key map passed to <c>registerShortcuts</c> / <c>updateShortcuts</c>.</summary>
    /// <param name="catalogue">The catalogue.</param>
    /// <param name="preferences">The shortcut preferences (validated).</param>
    /// <param name="scope">Scope (<c>global</c>, <c>mixer</c>, <c>library</c>, <c>setlist</c>).</param>
    /// <returns>The key map.</returns>
    public static KeyMap Build(KeyMapCatalogue catalogue, ShortcutPreferences preferences, string scope)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(preferences);
        var rows = catalogue.Commands.Where(c => c.Scope == scope).ToList();
        var keys = rows.Select(c => Binding(c, KeysChords(c, preferences), KeyBinding.KeysLayer));
        var pedal = rows.Select(c => (Row: c, Chords: PedalChords(catalogue, c, preferences)))
            .Where(p => p.Chords.Count > 0)
            .Select(p => Binding(p.Row, p.Chords, KeyBinding.PedalLayer) with { Anywhere = false });
        return new KeyMap([.. keys, .. pedal], preferences.SingleKey, preferences.PedalMode);
    }

    /// <summary>The keys-layer chords of a command.</summary>
    /// <param name="row">The catalogue row.</param>
    /// <param name="preferences">The shortcut preferences.</param>
    /// <returns>The chords.</returns>
    public static IReadOnlyList<KeyChordData> KeysChords(KeyMapCommand row, ShortcutPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(preferences);
        if (!preferences.Overrides.TryGetValue(row.Command, out var own) || (own.Count > 0 && own.All(c => c.Layer == KeyLayer.Pedal)))
        {
            return row.Defaults;
        }

        return [.. own.Where(c => c.Layer == KeyLayer.Keys).Select(c => c.Chord)];
    }

    /// <summary>The pedal-layer chords of a command.</summary>
    /// <param name="catalogue">The catalogue.</param>
    /// <param name="row">The catalogue row.</param>
    /// <param name="preferences">The shortcut preferences.</param>
    /// <returns>The chords (empty when the command has no pedal).</returns>
    public static IReadOnlyList<KeyChordData> PedalChords(KeyMapCatalogue catalogue, KeyMapCommand row, ShortcutPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(row);
        var own = PedalPresets.PedalChords(preferences, row.Command);
        if (own.Count > 0)
        {
            return [.. own.Select(c => c.Chord)];
        }

        var preset = PedalPresets.Current(catalogue, preferences);
        return [.. new[] { (preset.Left, catalogue.Pedal.LeftKey), (preset.Right, catalogue.Pedal.RightKey) }
            .Where(side => side.Item1 == row.Command).Select(side => side.Item2)];
    }

    private static KeyBinding Binding(KeyMapCommand row, IReadOnlyList<KeyChordData> chords, string layer) =>
        new(row.Command, chords, layer, row.Owner, row.Anywhere, row.Reserved);
}

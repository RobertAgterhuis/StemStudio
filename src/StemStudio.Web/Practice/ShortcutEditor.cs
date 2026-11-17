namespace StemStudio.Web.Practice;

/// <summary>The result of an edit on the shortcut settings page: the new preferences, or the reason it was refused.</summary>
/// <param name="Preferences">The preferences after the edit (unchanged when refused).</param>
/// <param name="Error">The message shown in <c>shortcut-conflict</c>, or null.</param>
public sealed record ShortcutEdit(ShortcutPreferences Preferences, string? Error);

/// <summary>
/// Edits of the shortcut settings page (FR-019, FR-021): assign a captured chord to a command (refused with a message
/// naming the other command or the reason), clear a command, reset everything. Pedal chords of a command are kept on
/// assign; clearing removes the command's own pedal chords too (its pedal falls back to the preset).
/// </summary>
public static class ShortcutEditor
{
    /// <summary>Assigns a chord as the only key of a command.</summary>
    /// <param name="catalogue">The catalogue.</param>
    /// <param name="preferences">The current preferences.</param>
    /// <param name="command">The command id.</param>
    /// <param name="chord">The captured chord.</param>
    /// <returns>The edit.</returns>
    public static ShortcutEdit Assign(KeyMapCatalogue catalogue, ShortcutPreferences preferences, string command, KeyChordData chord)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(chord);
        if (KeyMapValidator.Rejection(catalogue, preferences.Overrides, command, chord) is { } reason)
        {
            return new ShortcutEdit(preferences, Message(catalogue, chord, reason));
        }

        var pedal = PedalPresets.PedalChords(preferences, command).Take(1);
        return new ShortcutEdit(With(preferences, command, [new ShortcutChord(chord.Key, chord.Mod, chord.Shift), .. pedal]), null);
    }

    /// <summary>Unbinds the keys of a command.</summary>
    /// <param name="preferences">The current preferences.</param>
    /// <param name="command">The command id.</param>
    /// <returns>The new preferences.</returns>
    public static ShortcutPreferences Clear(ShortcutPreferences preferences, string command) => With(preferences, command, []);

    /// <summary>Every command back to its default keys (single-key switch, pedal mode and preset stay).</summary>
    /// <param name="preferences">The current preferences.</param>
    /// <returns>The new preferences.</returns>
    public static ShortcutPreferences ResetAll(ShortcutPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        return preferences with { Overrides = new Dictionary<string, IReadOnlyList<ShortcutChord>>(StringComparer.Ordinal) };
    }

    /// <summary>The label of a command, or the id.</summary>
    /// <param name="catalogue">The catalogue.</param>
    /// <param name="command">The command id.</param>
    /// <returns>The label.</returns>
    public static string Label(KeyMapCatalogue catalogue, string command)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        return catalogue.Commands.FirstOrDefault(c => c.Command == command)?.Label ?? command;
    }

    private static string Message(KeyMapCatalogue catalogue, KeyChordData chord, string reason) =>
        reason.StartsWith(KeyMapValidator.ConflictPrefix, StringComparison.Ordinal)
            ? $"{KeyChords.Display(chord)} is already used by \"{Label(catalogue, reason[KeyMapValidator.ConflictPrefix.Length..])}\". Change that one first."
            : $"{KeyChords.Display(chord)} cannot be used: {reason}.";

    private static ShortcutPreferences With(ShortcutPreferences preferences, string command, IReadOnlyList<ShortcutChord> chords)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var overrides = new Dictionary<string, IReadOnlyList<ShortcutChord>>(preferences.Overrides, StringComparer.Ordinal) { [command] = chords };
        return preferences with { Overrides = overrides };
    }
}

using System.Text.Json;

namespace StemStudio.Web.Practice;

/// <summary>
/// Validates the user's shortcut overrides (014 data-model 5.1, FR-019, FR-021) with the same rules as
/// <c>src/web-interop/src/overrides.ts</c> (both run the shared cases <c>keymap-override-cases.json</c>):
/// a command entry is kept only for a known, non-reserved catalogue command with at most 2 chords (at most 60 commands);
/// a chord is dropped when its shape is invalid, it is reserved or handled elsewhere, it is the plain <c>g</c> of the
/// "G then X" sequences, or it clashes with a chord already taken (first the defaults of every command without an
/// override, then each accepted chord in order). An overridden command never falls back to its defaults.
/// </summary>
public static class KeyMapValidator
{
    /// <summary>Most commands with an override.</summary>
    public const int MaxOverrides = 60;

    /// <summary>Most chords per command.</summary>
    public const int MaxChords = 2;

    /// <summary>Reason of a dropped command entry.</summary>
    public const string InvalidCommand = "not an overridable command or too many chords";

    /// <summary>Reason of a malformed chord.</summary>
    public const string InvalidChord = "invalid chord";

    /// <summary>Reason for the plain <c>g</c>.</summary>
    public const string SequenceReason = "Starts the \"G then\" sequences";

    /// <summary>Prefix of a conflict reason, followed by the other command id.</summary>
    public const string ConflictPrefix = "conflicts with ";

    private static readonly KeyChordData SequenceKey = new("g");

    /// <summary>Validates a raw <c>shortcuts.overrides</c> value.</summary>
    /// <param name="catalogue">The default key map catalogue.</param>
    /// <param name="raw">The parsed value (anything; a non-object gives no overrides).</param>
    /// <returns>The accepted overrides and the dropped entries.</returns>
    public static OverridesResult Validate(KeyMapCatalogue catalogue, JsonElement? raw)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        var entries = raw is { ValueKind: JsonValueKind.Object } value ? value.EnumerateObject().ToList() : [];
        var kept = Kept(catalogue, entries);
        var dropped = entries.Where(e => !kept.Exists(k => k.Name == e.Name)).Select(e => $"{e.Name}: {InvalidCommand}").ToList();
        var taken = DefaultsOfOthers(catalogue, kept.Select(k => k.Name).ToHashSet(StringComparer.Ordinal));
        var overrides = new Dictionary<string, IReadOnlyList<ShortcutChord>>(StringComparer.Ordinal);
        foreach (var entry in kept)
        {
            overrides[entry.Name] = Accept(catalogue, entry, taken, dropped);
        }

        return new OverridesResult(overrides, dropped);
    }

    /// <summary>Validates typed overrides (the settings page) with the same rules.</summary>
    /// <param name="catalogue">The default key map catalogue.</param>
    /// <param name="overrides">Command id to chords.</param>
    /// <returns>The accepted overrides and the dropped entries.</returns>
    public static OverridesResult Validate(KeyMapCatalogue catalogue, IReadOnlyDictionary<string, IReadOnlyList<ShortcutChord>> overrides) =>
        Validate(catalogue, ShortcutJson.OverridesElement(overrides));

    /// <summary>Why a chord cannot be assigned to a command given the other commands' current chords, or null.</summary>
    /// <param name="catalogue">The default key map catalogue.</param>
    /// <param name="overrides">The current overrides.</param>
    /// <param name="command">The command being edited.</param>
    /// <param name="chord">The chord to assign.</param>
    /// <returns>The reason (reserved reason, sequence reason or <c>conflicts with &lt;command&gt;</c>), or null.</returns>
    public static string? Rejection(
        KeyMapCatalogue catalogue, IReadOnlyDictionary<string, IReadOnlyList<ShortcutChord>> overrides, string command, KeyChordData chord)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(overrides);
        var taken = catalogue.Commands.Where(c => c.Command != command)
            .SelectMany(c => ChordsOf(c, overrides).Select(d => (d, c.Command)))
            .ToList();
        return Blocked(catalogue, chord) ?? Conflict(chord, taken);
    }

    /// <summary>The reason text when a chord can never be bound (reserved, or handled by another feature), else null.</summary>
    /// <param name="catalogue">The default key map catalogue.</param>
    /// <param name="chord">A chord.</param>
    /// <returns>The reason, or null.</returns>
    public static string? ReservedReason(KeyMapCatalogue catalogue, KeyChordData chord)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        if (catalogue.ReservedChords.FirstOrDefault(r => KeyChords.Clash(new KeyChordData(r.Key, r.Mod, r.Shift), chord)) is { } reserved)
        {
            return reserved.Reason;
        }

        return catalogue.ExternalChords.FirstOrDefault(e => e.Chord is { } c && KeyChords.Clash(c, chord)) is { } external
            ? $"Used elsewhere: {external.Label}"
            : null;
    }

    private static IEnumerable<KeyChordData> ChordsOf(KeyMapCommand command, IReadOnlyDictionary<string, IReadOnlyList<ShortcutChord>> overrides) =>
        overrides.TryGetValue(command.Command, out var own) ? own.Select(o => o.Chord) : command.Defaults;

    private static List<JsonProperty> Kept(KeyMapCatalogue catalogue, List<JsonProperty> entries)
    {
        var overridable = catalogue.Commands.Where(c => !c.Reserved).Select(c => c.Command).ToHashSet(StringComparer.Ordinal);
        return [.. entries.Where(e => overridable.Contains(e.Name) && e.Value is { ValueKind: JsonValueKind.Array } list && list.GetArrayLength() <= MaxChords)
            .Take(MaxOverrides)];
    }

    private static List<(KeyChordData Chord, string Command)> DefaultsOfOthers(KeyMapCatalogue catalogue, HashSet<string> overridden) =>
        [.. catalogue.Commands.Where(c => !overridden.Contains(c.Command)).SelectMany(c => c.Defaults.Select(d => (d, c.Command)))];

    private static List<ShortcutChord> Accept(KeyMapCatalogue catalogue, JsonProperty entry, List<(KeyChordData Chord, string Command)> taken, List<string> dropped)
    {
        var accepted = new List<ShortcutChord>();
        foreach (var value in entry.Value.EnumerateArray())
        {
            var chord = ShortcutJson.ReadChord(value);
            var reason = chord is null ? InvalidChord : Blocked(catalogue, chord.Chord) ?? Conflict(chord.Chord, taken);
            if (reason is null)
            {
                accepted.Add(chord!);
                taken.Add((chord!.Chord, entry.Name));
            }
            else
            {
                dropped.Add($"{entry.Name}: {reason}");
            }
        }

        return accepted;
    }

    private static string? Blocked(KeyMapCatalogue catalogue, KeyChordData chord) =>
        ReservedReason(catalogue, chord) ?? (KeyChords.Clash(SequenceKey, chord) ? SequenceReason : null);

    private static string? Conflict(KeyChordData chord, List<(KeyChordData Chord, string Command)> taken) =>
        taken.FirstOrDefault(t => KeyChords.Clash(t.Chord, chord)) is { Command: { } command } ? ConflictPrefix + command : null;
}

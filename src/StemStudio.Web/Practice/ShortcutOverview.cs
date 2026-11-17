namespace StemStudio.Web.Practice;

/// <summary>One row of the shortcut overview or settings: a command with its current keys and pedal keys.</summary>
/// <param name="Command">Command id.</param>
/// <param name="Label">Label.</param>
/// <param name="Keys">Keys-layer chords.</param>
/// <param name="Pedal">Pedal-layer chords.</param>
public sealed record ShortcutLine(string Command, string Label, IReadOnlyList<KeyChordData> Keys, IReadOnlyList<KeyChordData> Pedal)
{
    /// <summary>Test-id form of the command (<c>:</c> and <c>.</c> become <c>-</c>; ui-test-ids 1.13.0).</summary>
    public string TestId => Command.Replace(':', '-').Replace('.', '-');

    /// <summary>True for a fixed two-key sequence ("G then I"): <see cref="Keys"/> are the keys in order.</summary>
    public bool IsSequence { get; init; }

    /// <summary>The <c>data-keys</c> value: every chord, keys then pedal, joined by <c>|</c> (for example <c>n|pagedown</c>; <c>g i</c> for a sequence).</summary>
    public string DataKeys => IsSequence ? string.Join(' ', Keys.Select(Wire)) : string.Join('|', Keys.Concat(Pedal).Select(Wire));

    /// <summary>The visible keys ("N or PageDown (pedal)"; "-" when unbound).</summary>
    public string Text
    {
        get
        {
            if (IsSequence)
            {
                return string.Join(" then ", Keys.Select(KeyChords.Display));
            }

            var parts = Keys.Select(KeyChords.Display).Concat(Pedal.Select(p => $"{KeyChords.Display(p)} (pedal)")).ToList();
            return parts.Count == 0 ? "-" : string.Join(" or ", parts);
        }
    }

    /// <summary>The wire form of a chord (<c>mod+shift+s</c>, <c>space</c>).</summary>
    /// <param name="chord">The chord.</param>
    /// <returns>The text.</returns>
    public static string Wire(KeyChordData chord)
    {
        ArgumentNullException.ThrowIfNull(chord);
        return (chord.Mod ? "mod+" : string.Empty) + (chord.Shift == true ? "shift+" : string.Empty) + (chord.Key == " " ? "space" : chord.Key);
    }
}

/// <summary>A group of the overview (catalogue group order).</summary>
/// <param name="Group">Group id (<c>transport</c>, <c>loop-sections</c>, ...).</param>
/// <param name="Title">Title.</param>
/// <param name="Lines">The rows.</param>
public sealed record ShortcutGroup(string Group, string Title, IReadOnlyList<ShortcutLine> Lines);

/// <summary>
/// The live shortcut overview (014 FR-024): the current bindings of the given scopes, grouped as in the catalogue, with
/// remapped and pedal keys; reserved rows are hidden; the fixed "G then X" sequences are listed in navigation.
/// </summary>
public static class ShortcutOverview
{
    private static readonly Dictionary<string, string> Titles = new(StringComparer.Ordinal)
    {
        ["transport"] = "Play and save",
        ["loop-sections"] = "Loop and sections",
        ["speed"] = "Speed, key and click",
        ["channels"] = "Channels",
        ["setlist"] = "Setlist",
        ["score"] = "Score",
        ["navigation"] = "Everywhere",
    };

    /// <summary>The groups of the given scopes that have at least one row.</summary>
    /// <param name="catalogue">The catalogue.</param>
    /// <param name="preferences">The shortcut preferences.</param>
    /// <param name="scopes">Scopes shown (<c>global</c>, <c>mixer</c>, ...).</param>
    /// <returns>The groups in catalogue order.</returns>
    public static IReadOnlyList<ShortcutGroup> Build(KeyMapCatalogue catalogue, ShortcutPreferences preferences, IReadOnlyCollection<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(scopes);
        var lines = catalogue.Commands.Where(c => !c.Reserved && scopes.Contains(c.Scope))
            .Select(c => (c.Group, Line: new ShortcutLine(c.Command, c.Label, KeyMapBuilder.KeysChords(c, preferences), KeyMapBuilder.PedalChords(catalogue, c, preferences))))
            .Concat(Sequences(catalogue, scopes))
            .ToList();
        return [.. catalogue.Groups.Select(g => new ShortcutGroup(g, Title(g), [.. lines.Where(l => l.Group == g).Select(l => l.Line)]))
            .Where(g => g.Lines.Count > 0)];
    }

    /// <summary>The title of a group.</summary>
    /// <param name="group">Group id.</param>
    /// <returns>The title.</returns>
    public static string Title(string group) => Titles.TryGetValue(group, out var title) ? title : group;

    private static IEnumerable<(string Group, ShortcutLine Line)> Sequences(KeyMapCatalogue catalogue, IReadOnlyCollection<string> scopes) =>
        catalogue.Sequences.Where(s => scopes.Contains(s.Scope))
            .Select(s => (s.Group, new ShortcutLine(s.Command, s.Label, [.. s.Keys.Select(k => new KeyChordData(k))], []) { IsSequence = true }));
}

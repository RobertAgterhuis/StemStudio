namespace StemStudio.Web.Practice;

/// <summary>Chord rules shared by the validator and the key map builder (the same rules as <c>src/web-interop/src/chords.ts</c>).</summary>
public static class KeyChords
{
    /// <summary>Longest key name of a chord.</summary>
    public const int MaxKeyLength = 20;

    /// <summary>The Shift rule: given, else forbidden for letters and not checked for other keys.</summary>
    /// <param name="chord">A chord.</param>
    /// <returns>True (required), false (forbidden) or null (not checked).</returns>
    public static bool? EffectiveShift(KeyChordData chord)
    {
        ArgumentNullException.ThrowIfNull(chord);
        return chord.Shift ?? (chord.Key is [>= 'a' and <= 'z'] ? false : null);
    }

    /// <summary>True when two chords can be triggered by the same key press.</summary>
    /// <param name="a">First chord.</param>
    /// <param name="b">Second chord.</param>
    /// <returns>Whether they clash.</returns>
    public static bool Clash(KeyChordData a, KeyChordData b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var (sa, sb) = (EffectiveShift(a), EffectiveShift(b));
        return a.Key == b.Key && a.Mod == b.Mod && (sa is null || sb is null || sa == sb);
    }

    /// <summary>The display form of a chord (<c>Ctrl+Shift+S</c>, <c>Space</c>, <c>PageDown</c>, <c>L</c>).</summary>
    /// <param name="chord">A chord.</param>
    /// <returns>The text shown in the overview and the settings.</returns>
    public static string Display(KeyChordData chord)
    {
        ArgumentNullException.ThrowIfNull(chord);
        var parts = new List<string>(3);
        if (chord.Mod)
        {
            parts.Add("Ctrl");
        }

        if (chord.Shift == true)
        {
            parts.Add("Shift");
        }

        parts.Add(KeyName(chord.Key));
        return string.Join('+', parts);
    }

    private static string KeyName(string key) => key switch
    {
        " " => "Space",
        "arrowleft" => "Left",
        "arrowright" => "Right",
        "arrowup" => "Up",
        "arrowdown" => "Down",
        "pageup" => "PageUp",
        "pagedown" => "PageDown",
        [var single] => char.ToUpperInvariant(single).ToString(),
        _ => char.ToUpperInvariant(key[0]) + key[1..],
    };
}

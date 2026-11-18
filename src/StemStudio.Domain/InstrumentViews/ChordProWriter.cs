using System.Globalization;
using System.Text;
using StemStudio.Domain.Scores;

namespace StemStudio.Domain.InstrumentViews;

/// <summary>Song facts of a ChordPro file.</summary>
/// <param name="Title">Title.</param>
/// <param name="Artist">Artist, or null.</param>
/// <param name="Key">The global key at concert pitch, or null.</param>
public sealed record ChordProHeader(string Title, string? Artist, MusicalKey? Key);

/// <summary>
/// Writes a chord sheet as ChordPro (data-model 2.6): <c>{title: }</c>, <c>{artist: }</c>, <c>{capo: n}</c> when above 0,
/// <c>{key: }</c> (the key the shapes are played in), inline <c>[Am]</c> chords; instrumental rows as <c>[C] [G]</c>.
/// Text never opens a directive or a chord: brackets and braces in titles and lyrics become parentheses, line breaks spaces.
/// </summary>
public static class ChordProWriter
{
    /// <summary>Writes the file text (LF line ends, final LF).</summary>
    /// <param name="header">Title, artist and key.</param>
    /// <param name="sheet">The sheet.</param>
    /// <returns>ChordPro text.</returns>
    public static string Write(ChordProHeader header, ChordSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(sheet);
        var text = new StringBuilder();
        Directive(text, "title", header.Title);
        if (!string.IsNullOrWhiteSpace(header.Artist))
        {
            Directive(text, "artist", header.Artist);
        }

        if (sheet.Display.Capo > 0)
        {
            Directive(text, "capo", sheet.Display.Capo.ToString(CultureInfo.InvariantCulture));
        }

        if (ChordSpeller.Move(header.Key, sheet.Display) is { } key)
        {
            Directive(text, "key", KeySymbol(key));
        }

        foreach (var row in sheet.Rows)
        {
            text.Append(Row(row)).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>Makes free text safe inside a ChordPro line: no brackets, braces or line breaks.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The safe text.</returns>
    public static string Safe(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var safe = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            safe.Append(character switch
            {
                '[' or '{' => '(',
                ']' or '}' => ')',
                _ when char.IsControl(character) => ' ',
                _ => character,
            });
        }

        return safe.ToString();
    }

    private static void Directive(StringBuilder text, string name, string value) => text.Append('{').Append(name).Append(": ").Append(Safe(value).Trim()).Append("}\n");

    private static string KeySymbol(MusicalKey key) =>
        ChordSpeller.NoteName(key.Tonic, ChordSpeller.UsesFlats(key)) + (key.Mode == KeyMode.Minor ? "m" : string.Empty);

    private static string Row(SheetRow row)
    {
        if (row.Text.Length == 0)
        {
            return string.Join(' ', row.Chords.Select(chord => $"[{chord.Symbol}]"));
        }

        var text = Safe(row.Text);
        var line = new StringBuilder();
        var leads = row.Chords.Where(chord => chord.Lead).Select(chord => $"[{chord.Symbol}]");
        line.Append(string.Concat(leads));
        var cursor = 0;
        foreach (var chord in row.Chords.Where(chord => !chord.Lead))
        {
            var at = Math.Clamp(chord.CharIndex, cursor, text.Length);
            line.Append(text, cursor, at - cursor).Append('[').Append(chord.Symbol).Append(']');
            cursor = at;
        }

        return line.Append(text, cursor, text.Length - cursor).ToString();
    }
}

namespace StemStudio.Web.Practice;

/// <summary>Validated shortcut overrides plus one line per dropped command or chord (<c>&lt;command&gt;: &lt;reason&gt;</c>).</summary>
/// <param name="Overrides">Command id to accepted chords, in record order.</param>
/// <param name="Dropped">The dropped entries.</param>
public sealed record OverridesResult(IReadOnlyDictionary<string, IReadOnlyList<ShortcutChord>> Overrides, IReadOnlyList<string> Dropped);

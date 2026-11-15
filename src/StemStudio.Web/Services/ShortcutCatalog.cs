namespace StemStudio.Web.Services;

/// <summary>One row of the shortcut overlay.</summary>
/// <param name="Keys">The keys, e.g. <c>Mod+K</c>.</param>
/// <param name="Action">What they do.</param>
public sealed record ShortcutRow(string Keys, string Action);

/// <summary>The keyboard map of design-spec.md 6 as shown in the shortcut overlay and the help topic.</summary>
public static class ShortcutCatalog
{
    /// <summary>Keys valid on every screen.</summary>
    public static IReadOnlyList<ShortcutRow> Global { get; } =
    [
        new("Mod+K", "Command palette"),
        new("? or F1", "Help for what you are on"),
        new("Mod+/", "This overview (Expert)"),
        new("G then I, T or M", "Go to Import, Tracks or Mixes"),
        new("Esc", "Close the panel, palette or tour"),
    ];

    /// <summary>Keys of the mixer.</summary>
    public static IReadOnlyList<ShortcutRow> Mixer { get; } =
    [
        new("Space", "Play or pause"),
        new("Home", "Stop and go to the start"),
        new("Left / Right", "Seek 5 s back or forward (Shift: 1 s)"),
        new("[ and ]", "Set loop A or B at the position"),
        new("L", "Loop on or off"),
        new("K", "Click on or off"),
        new("1 to 9", "Select a channel"),
        new("M / S", "Mute or solo the selected channel"),
        new("Mod+S", "Save the mix (Shift: save as)"),
    ];
}

using System.Globalization;

namespace StemStudio.Web.Services;

/// <summary>A per-channel action row of the track page: "Save to library" (feature 007) or "Sheet music" (feature 008).</summary>
/// <param name="Prefix">Test id prefix: <c>&lt;prefix&gt;-stem-&lt;stem&gt;</c> and <c>&lt;prefix&gt;-detail-&lt;parent&gt;-&lt;detail&gt;</c>.</param>
/// <param name="Caption">Visible caption in front of the buttons.</param>
/// <param name="Icon">Material icon of every button.</param>
/// <param name="AriaFormat">Accessible name with <c>{0}</c> for the channel label.</param>
/// <param name="HelpId">Help marker of the row.</param>
public sealed record ChannelAction(string Prefix, string Caption, string Icon, string AriaFormat, string HelpId)
{
    /// <summary>Raw export to the library (ui-test-ids 1.5.0).</summary>
    public static ChannelAction Export { get; } = new("export", "Save to library:", "library_add", "Save {0} to the library", "export-actions");

    /// <summary>Sheet music (ui-test-ids 1.7.0).</summary>
    public static ChannelAction Score { get; } = new("score", "Sheet music:", "music_note", "Sheet music of {0}", "sheet-music");

    /// <summary>The test id of a channel's button.</summary>
    /// <param name="channel">A stem (<c>bass</c>) or detail channel (<c>drums.snare</c>).</param>
    /// <returns>For example <c>export-detail-drums-snare</c>.</returns>
    public string TestId(string channel) =>
        channel.Contains('.', StringComparison.Ordinal) ? $"{Prefix}-detail-{channel.Replace('.', '-')}" : $"{Prefix}-stem-{channel}";

    /// <summary>The accessible name of a channel's button.</summary>
    /// <param name="label">The channel label.</param>
    /// <returns>The name.</returns>
    public string AriaLabel(string label) => string.Format(CultureInfo.InvariantCulture, AriaFormat, label);
}

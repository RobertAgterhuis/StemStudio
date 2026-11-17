using System.Text.Json;
using Microsoft.JSInterop;
using StemStudio.Web.Preferences;

namespace StemStudio.Web.Lyrics;

/// <summary>The banner choices of this browser (006 FR-009): shown or hidden, and the text size.</summary>
/// <param name="Visible">True while the banner is shown.</param>
/// <param name="Size"><c>s</c>, <c>m</c> or <c>l</c>.</param>
public sealed record KaraokeSettings(bool Visible, string Size)
{
    /// <summary>The text sizes in display order.</summary>
    public static IReadOnlyList<string> Sizes { get; } = ["s", "m", "l"];

    /// <summary>Shown, medium text.</summary>
    public static KaraokeSettings Default { get; } = new(true, "m");
}

/// <summary>
/// Keeps <see cref="KaraokeSettings"/> in <c>localStorage["stemstudio.karaoke"]</c>. Storage may be unavailable (private
/// mode, blocked site data): reads then give the defaults and writes are skipped, so the banner always works.
/// </summary>
/// <param name="js">The circuit's JS runtime.</param>
public sealed class KaraokePreferences(IJSRuntime js)
{
    /// <summary>The local-storage key.</summary>
    public const string StorageKey = "stemstudio.karaoke";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Reads the stored choices.</summary>
    /// <returns>The choices, or the defaults when nothing usable is stored.</returns>
    public async Task<KaraokeSettings> LoadAsync() => Parse(await BrowserStorage.GetAsync(js, StorageKey));

    /// <summary>Stores the choices (best effort).</summary>
    /// <param name="settings">The choices.</param>
    /// <returns>A task that completes when the write was tried.</returns>
    public Task SaveAsync(KaraokeSettings settings) => BrowserStorage.SetAsync(js, StorageKey, JsonSerializer.Serialize(settings, Json));

    private static KaraokeSettings Parse(string? stored)
    {
        try
        {
            var settings = stored is null ? null : JsonSerializer.Deserialize<StoredSettings>(stored, Json);
            return settings is null ? KaraokeSettings.Default : new(settings.Visible ?? true, Valid(settings.Size));
        }
        catch (JsonException)
        {
            return KaraokeSettings.Default;
        }
    }

    private static string Valid(string? size) => size is not null && KaraokeSettings.Sizes.Contains(size) ? size : KaraokeSettings.Default.Size;

    private sealed record StoredSettings(bool? Visible, string? Size);
}

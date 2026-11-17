using Microsoft.JSInterop;

namespace StemStudio.Web.Preferences;

/// <summary>
/// Best-effort access to <c>window.localStorage</c> for small per-browser UI choices (karaoke banner, practice panel).
/// Storage may be unavailable (private mode, blocked site data, prerendering, a closed circuit): reads then give null and
/// writes are skipped, so the UI always works with its defaults.
/// </summary>
public static class BrowserStorage
{
    /// <summary>Reads a stored value.</summary>
    /// <param name="js">The circuit's JS runtime.</param>
    /// <param name="key">The local-storage key.</param>
    /// <returns>The stored text, or null when nothing is stored or storage is unavailable.</returns>
    public static async Task<string?> GetAsync(IJSRuntime js, string key)
    {
        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            return null;
        }
    }

    /// <summary>Stores a value (best effort).</summary>
    /// <param name="js">The circuit's JS runtime.</param>
    /// <param name="key">The local-storage key.</param>
    /// <param name="value">The text to store.</param>
    /// <returns>A task that completes when the write was tried.</returns>
    public static async Task SetAsync(IJSRuntime js, string key, string value)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", key, value);
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            // Storage is unavailable; the choice lasts for this visit only.
        }
    }

    private static bool IsUnavailable(Exception ex) => ex is JSException or JSDisconnectedException or InvalidOperationException;
}

using System.Text.Json;
using Microsoft.JSInterop;
using StemStudio.Web.Preferences;

namespace StemStudio.Web.InstrumentViews;

/// <summary>
/// Browser preferences of the instrument views (data-model 8): <c>localStorage</c>, best effort, defaults when storage is
/// unavailable or holds something unusable. Nothing here is sent to the server.
/// </summary>
/// <param name="js">The circuit's JS runtime.</param>
public sealed class ViewPreferences(IJSRuntime js)
{
    /// <summary>Last "Practise as" family (offered, not applied).</summary>
    public const string FamilyKey = "stemstudio.views.family";

    /// <summary>Capo mode per track id: <c>{capo, showSounding}</c>.</summary>
    public const string CapoKey = "stemstudio.chords.capo";

    /// <summary>Written instrument of the orchestral family.</summary>
    public const string InstrumentKey = "stemstudio.views.instrument";

    /// <summary>Chord lane shown (default true).</summary>
    public const string LaneKey = "stemstudio.chords.lane";

    /// <summary>Look-ahead of the drum lanes in ms.</summary>
    public const string LookAheadKey = "stemstudio.drums.lookAheadMs";

    /// <summary>Singing tolerance in cents.</summary>
    public const string ToleranceKey = "stemstudio.singing.toleranceCents";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The key of the stored flags of "Drumless" for a track.</summary>
    /// <param name="trackId">The track.</param>
    /// <returns>The key.</returns>
    public static string DrumlessKey(Guid trackId) => $"stemstudio.drumless.previous.{trackId}";

    /// <summary>Reads a stored JSON value.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The value, or default when nothing usable is stored.</returns>
    public async Task<T?> GetAsync<T>(string key)
    {
        var stored = await BrowserStorage.GetAsync(js, key);
        try
        {
            return stored is null ? default : JsonSerializer.Deserialize<T>(stored, Json);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>Stores a JSON value (best effort).</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <returns>A task that completes when the write was tried.</returns>
    public Task SetAsync<T>(string key, T value) => BrowserStorage.SetAsync(js, key, JsonSerializer.Serialize(value, Json));

    /// <summary>Removes a value (best effort).</summary>
    /// <param name="key">The key.</param>
    /// <returns>A task that completes when the removal was tried.</returns>
    public async Task RemoveAsync(string key)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", key);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
            // Storage is unavailable; nothing to remove.
        }
    }
}

using System.Text.Json;

namespace StemStudio.Web.Interop;

/// <summary>Result of <c>readPreferences()</c> (web-interop.md 2 <c>PreferencesRead</c>).</summary>
/// <param name="Preferences">The validated stored record (data-model.md 1.1) as a JSON object.</param>
/// <param name="StorageAvailable">False when local storage threw on access.</param>
public sealed record StoredPreferencesRead(JsonElement Preferences, bool StorageAvailable);

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace StemStudio.Web.Preferences;

/// <summary>Record mapping. Red stub (T015).</summary>
public static class PreferencesJson
{
    /// <summary>To JSON.</summary>
    /// <param name="preferences">Preferences.</param>
    /// <returns>JSON.</returns>
    public static JsonElement ToElement(UserPreferences preferences) => throw new NotImplementedException();

    /// <summary>From JSON.</summary>
    /// <param name="record">JSON.</param>
    /// <returns>Preferences.</returns>
    public static UserPreferences FromElement(JsonElement record) => throw new NotImplementedException();

    /// <summary>Parse.</summary>
    /// <param name="json">JSON.</param>
    /// <param name="preferences">Result.</param>
    /// <returns>Valid.</returns>
    public static bool TryParse(string json, [NotNullWhen(true)] out UserPreferences? preferences) => throw new NotImplementedException();
}

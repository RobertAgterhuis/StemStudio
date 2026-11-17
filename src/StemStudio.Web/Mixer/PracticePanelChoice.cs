using System.Text.Json;

namespace StemStudio.Web.Mixer;

/// <summary>
/// The practice panel's open/closed choice of this browser (012 integration, CTO decision 2026-11-17), kept in
/// <c>localStorage["stemstudio.practice-panel"]</c> as <c>{"expanded":true}</c>. Without a usable stored choice the panel
/// is open only while the practice settings are not neutral (<see cref="IsOpenByDefault"/>).
/// </summary>
public static class PracticePanelChoice
{
    /// <summary>The local-storage key.</summary>
    public const string StorageKey = "stemstudio.practice-panel";

    /// <summary>Reads a stored value.</summary>
    /// <param name="stored">The stored text, or null.</param>
    /// <returns>The stored choice, or null when nothing usable is stored.</returns>
    public static bool? Parse(string? stored)
    {
        try
        {
            using var document = JsonDocument.Parse(stored ?? "null");
            return Expanded(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool? Expanded(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty("expanded", out var expanded) && IsBoolean(expanded) ? expanded.GetBoolean() : null;

    private static bool IsBoolean(JsonElement value) => value.ValueKind is JsonValueKind.True or JsonValueKind.False;

    /// <summary>The stored form of a choice.</summary>
    /// <param name="expanded">True when the panel is open.</param>
    /// <returns>The JSON text.</returns>
    public static string Serialize(bool expanded) => expanded ? "{\"expanded\":true}" : "{\"expanded\":false}";

    /// <summary>Open without a stored choice: the heard speed or key, the count-in or the speed trainer is not neutral.</summary>
    /// <param name="summary">What the header summary would show.</param>
    /// <returns>Whether the panel starts open.</returns>
    public static bool IsOpenByDefault(IReadOnlyCollection<string> summary) => summary.Count > 0;

    /// <summary>The non-neutral parts shown in the header, e.g. "75 %", "+2 st", "count-in", "trainer".</summary>
    /// <param name="speedPercent">The speed heard now.</param>
    /// <param name="transposeSemitones">The transpose heard now.</param>
    /// <param name="settings">The mixer's practice settings (count-in, trainer).</param>
    /// <returns>The parts in display order; empty when everything is neutral.</returns>
    public static IReadOnlyList<string> Summary(int speedPercent, int transposeSemitones, StemStudio.Contracts.Mixes.PracticeSettingsDto settings)
    {
        var parts = new List<string>(4);
        AddIf(parts, speedPercent != 100, PracticeText.Percent(speedPercent));
        AddIf(parts, transposeSemitones != 0, $"{PracticeText.Semitones(transposeSemitones)} st");
        AddIf(parts, settings.CountIn.Bars > 0, "count-in");
        AddIf(parts, settings.Trainer.Enabled, "trainer");
        return parts;
    }

    private static void AddIf(List<string> parts, bool condition, string part)
    {
        if (condition)
        {
            parts.Add(part);
        }
    }
}

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Web.Experience;
using StemStudio.Web.Practice;

namespace StemStudio.Web.Preferences;

/// <summary>
/// Maps <see cref="UserPreferences"/> to and from the stored record (data-model.md 1.1; schema version 2 with
/// <c>shortcuts</c>, 014 data-model 5.1). Version 1 records are read with default shortcuts; writes are version 2.
/// Reading validates field by field: an invalid or missing field takes its default; overrides follow
/// <see cref="KeyMapValidator"/>.
/// </summary>
public static class PreferencesJson
{
    private const string TimeFormat = "yyyy-MM-ddTHH:mm:ssZ";

    /// <summary>Builds the stored record.</summary>
    /// <param name="preferences">The preferences.</param>
    /// <returns>The record as a JSON object.</returns>
    public static JsonElement ToElement(UserPreferences preferences)
    {
        var record = new JsonObject
        {
            ["schemaVersion"] = 2,
            ["level"] = preferences.Level is { } level ? Lower(level) : null,
            ["theme"] = Lower(preferences.Theme),
            ["density"] = Lower(preferences.Density),
            ["motion"] = Lower(preferences.Motion),
            ["help"] = HelpNode(preferences.Help),
            ["shortcuts"] = ShortcutsNode(preferences.Shortcuts),
        };
        return JsonSerializer.SerializeToElement(record);
    }

    /// <summary>Reads a record; anything invalid takes its default (never throws).</summary>
    /// <param name="record">The record.</param>
    /// <returns>The preferences.</returns>
    public static UserPreferences FromElement(JsonElement record)
    {
        if (record.ValueKind != JsonValueKind.Object)
        {
            return UserPreferences.Default;
        }

        return new UserPreferences(
            Enum<ExperienceLevel>(record, "level"),
            Enum<ThemePreference>(record, "theme") ?? ThemePreference.System,
            Enum<DensityPreference>(record, "density") ?? DensityPreference.Auto,
            Enum<MotionPreference>(record, "motion") ?? MotionPreference.System,
            Help(Property(record, "help")))
        {
            Shortcuts = SchemaVersion(record) == "2" ? Shortcuts(Property(record, "shortcuts")) : ShortcutPreferences.Default,
        };
    }

    /// <summary>Reads the <c>shortcuts</c> field; invalid parts take their defaults, invalid overrides are dropped.</summary>
    /// <param name="shortcuts">The field.</param>
    /// <returns>The shortcut preferences.</returns>
    public static ShortcutPreferences Shortcuts(JsonElement? shortcuts)
    {
        var catalogue = KeyMapDefaults.Catalogue;
        var preset = Property(shortcuts ?? default, "pedalPreset") is { ValueKind: JsonValueKind.String } id
            && catalogue.Pedal.Presets.Any(p => p.Id == id.GetString()) ? id.GetString()! : catalogue.Pedal.DefaultPreset;
        return new ShortcutPreferences(
            Property(shortcuts ?? default, "singleKey") is not { ValueKind: JsonValueKind.False },
            Property(shortcuts ?? default, "pedalMode") is { ValueKind: JsonValueKind.True },
            preset,
            KeyMapValidator.Validate(catalogue, Property(shortcuts ?? default, "overrides")).Overrides);
    }

    /// <summary>Parses a record received from another tab.</summary>
    /// <param name="json">The JSON text.</param>
    /// <param name="preferences">The preferences when valid.</param>
    /// <returns>False for unparseable JSON, a non-object or another schema version.</returns>
    public static bool TryParse(string json, [NotNullWhen(true)] out UserPreferences? preferences)
    {
        preferences = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var valid = SchemaVersion(root) is "1" or "2";
            preferences = valid ? FromElement(root) : null;
            return valid;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? SchemaVersion(JsonElement record) =>
        Property(record, "schemaVersion") is { ValueKind: JsonValueKind.Number } version ? version.GetRawText() : null;

    private static JsonObject ShortcutsNode(ShortcutPreferences shortcuts) => new()
    {
        ["singleKey"] = shortcuts.SingleKey,
        ["pedalMode"] = shortcuts.PedalMode,
        ["pedalPreset"] = shortcuts.PedalPreset,
        ["overrides"] = ShortcutJson.OverridesNode(shortcuts.Overrides),
    };

    private static string Lower<T>(T value)
        where T : struct, Enum => value.ToString().ToLowerInvariant();

    private static JsonObject HelpNode(HelpProgress help)
    {
        var tours = new JsonObject();
        foreach (var (id, tour) in help.Tours)
        {
            tours[id] = new JsonObject
            {
                ["version"] = tour.Version,
                ["status"] = Lower(tour.Status),
                ["at"] = tour.At.UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture),
            };
        }

        return new JsonObject
        {
            ["welcomeDismissed"] = help.WelcomeDismissed,
            ["tours"] = tours,
            ["hintsDismissed"] = new JsonArray([.. help.HintsDismissed.Order(StringComparer.Ordinal).Select(h => (JsonNode)h)]),
        };
    }

    private static JsonElement? Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : null;

    private static T? Enum<T>(JsonElement record, string name)
        where T : struct, Enum =>
        Property(record, name) is { ValueKind: JsonValueKind.String } value
        && System.Enum.TryParse<T>(value.GetString(), ignoreCase: true, out var parsed)
        && System.Enum.IsDefined(parsed)
            ? parsed
            : null;

    private static HelpProgress Help(JsonElement? help)
    {
        if (help is not { ValueKind: JsonValueKind.Object } value)
        {
            return HelpProgress.Empty;
        }

        var dismissed = Property(value, "welcomeDismissed") is { ValueKind: JsonValueKind.True };
        return new HelpProgress(dismissed, Tours(Property(value, "tours")), Hints(Property(value, "hintsDismissed")));
    }

    private static Dictionary<string, TourRecord> Tours(JsonElement? tours)
    {
        var result = new Dictionary<string, TourRecord>(StringComparer.Ordinal);
        if (tours is not { ValueKind: JsonValueKind.Object } value)
        {
            return result;
        }

        foreach (var tour in value.EnumerateObject())
        {
            if (Tour(tour.Value) is { } record)
            {
                result[tour.Name] = record;
            }
        }

        return result;
    }

    private static TourRecord? Tour(JsonElement tour) =>
        (TourVersion(tour), Enum<TourStatus>(tour, "status"), TourTime(tour)) is ( > 0 and var version, { } status, { } at)
            ? new TourRecord(version, status, at)
            : null;

    private static int TourVersion(JsonElement tour) =>
        Property(tour, "version") is { ValueKind: JsonValueKind.Number } v && v.TryGetInt32(out var n) ? n : 0;

    private static DateTimeOffset? TourTime(JsonElement tour) =>
        Property(tour, "at") is { ValueKind: JsonValueKind.String } at
        && DateTimeOffset.TryParse(at.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time)
            ? time
            : null;

    private static HashSet<string> Hints(JsonElement? hints)
    {
        var values = hints is { ValueKind: JsonValueKind.Array } array
            ? array.EnumerateArray().Where(h => h.ValueKind == JsonValueKind.String).Select(h => h.GetString()!)
            : [];
        return new HashSet<string>(values, StringComparer.Ordinal);
    }
}

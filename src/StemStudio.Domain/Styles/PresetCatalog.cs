using System.Text.Json;
using System.Text.Json.Serialization;

namespace StemStudio.Domain.Styles;

/// <summary>The curated presets (embedded <c>presets.v1.json</c>, research.md R-04); seeded read-only at start-up.</summary>
public sealed class PresetCatalog
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private PresetCatalog(int version, IReadOnlyList<PresetDefinition> presets) => (Version, Presets) = (version, presets);

    /// <summary>Resource version.</summary>
    public int Version { get; }

    /// <summary>The presets in resource order.</summary>
    public IReadOnlyList<PresetDefinition> Presets { get; }

    /// <summary>Loads the embedded resource.</summary>
    /// <returns>The catalog.</returns>
    public static PresetCatalog Load() => Parse(EmbeddedText.Read("presets.v1.json"));

    /// <summary>Parses a preset document.</summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The catalog.</returns>
    /// <exception cref="InvalidOperationException">The document is empty.</exception>
    public static PresetCatalog Parse(string json)
    {
        var document = JsonSerializer.Deserialize<PresetDocument>(json, Json) ?? throw new InvalidOperationException("The preset resource is empty.");
        return new PresetCatalog(document.Version, document.Presets.Select(ToDefinition).ToList());
    }

    private static PresetDefinition ToDefinition(PresetEntry entry) => new(entry.Id, entry.Key, new StyleFields
    {
        Name = entry.Name,
        GenreFamily = entry.GenreFamily,
        SubGenre = entry.SubGenre,
        TempoFeel = entry.TempoFeel,
        Instrumentation = entry.Instrumentation,
        Moods = entry.Moods,
        VocalHandling = entry.VocalHandling,
        DefaultStrength = entry.DefaultStrength,
        Description = entry.Description,
    });

    private sealed record PresetDocument(int Version, IReadOnlyList<PresetEntry> Presets);

    private sealed class PresetEntry
    {
        public Guid Id { get; init; }

        public string Key { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public GenreFamily GenreFamily { get; init; }

        public string? SubGenre { get; init; }

        public TempoFeel TempoFeel { get; init; }

        public IReadOnlyList<string> Instrumentation { get; init; } = [];

        public IReadOnlyList<string> Moods { get; init; } = [];

        public VocalHandling VocalHandling { get; init; }

        public Strength DefaultStrength { get; init; }

        public string? Description { get; init; }
    }
}

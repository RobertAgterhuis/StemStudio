using System.Text.Json;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;

namespace StemStudio.Domain.Tests.InstrumentViews;

/// <summary>Reads the shared <c>chord-spelling-vectors.json</c> 1.0.0 (data-model 2.5, 2.8, 2.9).</summary>
internal static class SpellingVectors
{
    private static readonly Lazy<JsonElement> Root = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "InstrumentViews", "chord-spelling-vectors.json"))).RootElement.Clone());

    public static JsonElement Document => Root.Value;

    public static TheoryData<int> Indexes(string section)
    {
        var data = new TheoryData<int>();
        for (var index = 0; index < Root.Value.GetProperty(section).GetArrayLength(); index++)
        {
            data.Add(index);
        }

        return data;
    }

    public static JsonElement At(string section, int index) => Root.Value.GetProperty(section)[index];

    public static int? Int(JsonElement element, string name) =>
        element.GetProperty(name).ValueKind == JsonValueKind.Null ? null : element.GetProperty(name).GetInt32();

    public static string? Str(JsonElement element, string name) =>
        element.GetProperty(name).ValueKind == JsonValueKind.Null ? null : element.GetProperty(name).GetString();

    public static ChordQuality? Quality(JsonElement element) =>
        Str(element, "quality") is { } harte ? ChordQualities.FromHarte(harte) : null;

    public static KeyMode Mode(string mode) => mode == "major" ? KeyMode.Major : KeyMode.Minor;

    public static WrittenInstrument Instrument(string id) =>
        Enum.Parse<WrittenInstrument>(string.Concat(id.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part[1..])), ignoreCase: true);
}

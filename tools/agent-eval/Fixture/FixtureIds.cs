using System.Security.Cryptography;
using System.Text;

namespace StemStudio.AgentEval.Fixture;

/// <summary>
/// The ids of the seeded fixture (evaluation/eval-set.md "Seeded fixture"): every key (<c>t-hero</c>, <c>m-sandman</c>,
/// <c>g-song</c>, ...) maps to a stable id, so the model sees real UUIDs and the grader sees the keys again.
/// </summary>
internal sealed class FixtureIds
{
    private readonly Dictionary<string, Guid> _ids = new(StringComparer.Ordinal);

    /// <summary>The id of a key; a stable hash of the key unless a fixed id was registered (style presets).</summary>
    public Guid Of(string key)
    {
        if (!_ids.TryGetValue(key, out var id))
        {
            id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("stemstudio-eval:" + key)).AsSpan(0, 16));
            _ids[key] = id;
        }

        return id;
    }

    /// <summary>Registers a fixed id for a key (the preset ids of presets.v1.json).</summary>
    public void Register(string key, Guid id) => _ids[key] = id;

    /// <summary>Replaces every known id in the JSON text by its key (recorded call arguments before grading).</summary>
    public string ToKeys(string json) =>
        _ids.Aggregate(json, (text, pair) => text.Replace(pair.Value.ToString(), pair.Key, StringComparison.OrdinalIgnoreCase));

    /// <summary>Replaces every quoted known key in the JSON text by its id (scripted ideal transcripts).</summary>
    public string ToIds(string json) =>
        _ids.Aggregate(json, (text, pair) => text.Replace($"\"{pair.Key}\"", $"\"{pair.Value}\"", StringComparison.Ordinal));
}

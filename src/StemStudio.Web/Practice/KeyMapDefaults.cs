using System.Text.Json;
using Microsoft.Extensions.FileProviders;

namespace StemStudio.Web.Practice;

/// <summary>
/// Loads the default key map catalogue <c>wwwroot/js/keymap-defaults.json</c> (web-interop 1.1.0, feature 014 FR-020,
/// data-model 5.2): the single source of the default chords for C# and TypeScript. Architect skeleton (T006); the
/// frontend lane adds the validator and the merge with the user's overrides (T047).
/// </summary>
public static class KeyMapDefaults
{
    /// <summary>Path of the catalogue below the web root.</summary>
    public const string WebRootPath = "js/keymap-defaults.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Parses a catalogue document.</summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The catalogue.</returns>
    /// <exception cref="InvalidDataException">The text is not a catalogue.</exception>
    public static KeyMapCatalogue Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<KeyMapCatalogue>(json, Options) ?? throw new InvalidDataException("The key map catalogue is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The key map catalogue cannot be read.", exception);
        }
    }

    /// <summary>Reads and parses the catalogue from the web root.</summary>
    /// <param name="webRoot">The web root file provider (<c>IWebHostEnvironment.WebRootFileProvider</c>).</param>
    /// <returns>The catalogue.</returns>
    /// <exception cref="FileNotFoundException">The catalogue is missing.</exception>
    public static KeyMapCatalogue Load(IFileProvider webRoot)
    {
        ArgumentNullException.ThrowIfNull(webRoot);
        var file = webRoot.GetFileInfo(WebRootPath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("The key map catalogue is missing.", WebRootPath);
        }

        using var reader = new StreamReader(file.CreateReadStream());
        return Parse(reader.ReadToEnd());
    }
}

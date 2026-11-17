using System.Text.Json;
using Microsoft.Extensions.FileProviders;

namespace StemStudio.Web.Practice;

/// <summary>
/// Loads the default key map catalogue <c>wwwroot/js/keymap-defaults.json</c> (web-interop 1.1.0, feature 014 FR-020,
/// data-model 5.2): the single source of the default chords for C# and TypeScript. The validator is
/// <see cref="KeyMapValidator"/>, the merge with the user's overrides <see cref="KeyMapBuilder"/>.
/// </summary>
public static class KeyMapDefaults
{
    /// <summary>Path of the catalogue below the web root.</summary>
    public const string WebRootPath = "js/keymap-defaults.json";

    /// <summary>Manifest name of the embedded copy (<c>StemStudio.Web.csproj</c>).</summary>
    public const string ResourceName = "StemStudio.Web.keymap-defaults.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static readonly Lazy<KeyMapCatalogue> Embedded = new(() => Read(typeof(KeyMapDefaults).Assembly.GetManifestResourceStream(ResourceName)));

    /// <summary>The catalogue embedded in this assembly (the same file the browser module imports).</summary>
    public static KeyMapCatalogue Catalogue => Embedded.Value;

    /// <summary>Reads a catalogue from a stream.</summary>
    /// <param name="stream">The stream, or null when the resource is missing.</param>
    /// <returns>The catalogue.</returns>
    /// <exception cref="FileNotFoundException">The stream is null.</exception>
    internal static KeyMapCatalogue Read(Stream? stream)
    {
        if (stream is null)
        {
            throw new FileNotFoundException("The key map catalogue is missing.", ResourceName);
        }

        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

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

        return Read(file.CreateReadStream());
    }
}

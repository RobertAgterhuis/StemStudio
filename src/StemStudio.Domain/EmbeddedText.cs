namespace StemStudio.Domain;

/// <summary>Reads text resources embedded in the domain assembly (logical name = file name).</summary>
internal static class EmbeddedText
{
    /// <summary>The whole resource as UTF-8 text.</summary>
    /// <param name="name">Logical name, e.g. <c>presets.v1.json</c>.</param>
    /// <returns>The text.</returns>
    public static string Read(string name)
    {
        using var stream = typeof(EmbeddedText).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The resource {name} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>The non-blank lines of a resource that do not start with <c>#</c>, trimmed.</summary>
    /// <param name="name">Logical name.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<string> Lines(string name) =>
        Read(name).Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#')).ToList();
}

using System.Globalization;
using StemStudio.Domain.Generations;

namespace StemStudio.Api.Generations;

/// <summary>Readable names of generations and takes (009 data-model 3.6 title rule).</summary>
public static class GenerationText
{
    /// <summary>What a generation was made from: the title snapshot and a short description.</summary>
    /// <param name="generation">The generation.</param>
    /// <returns>Title and description.</returns>
    public static (string Title, string Description) Source(Generation generation)
    {
        ArgumentNullException.ThrowIfNull(generation);
        return generation.Restyle is { } restyle
            ? (restyle.SourceTrackTitle, $"restyle, {restyle.Style.Name}")
            : ("Generation", generation.Kind.ToString().ToLowerInvariant());
    }

    /// <summary>"&lt;source title&gt; - &lt;style name&gt; (take n)".</summary>
    /// <param name="generation">The generation.</param>
    /// <param name="index">The take number.</param>
    /// <returns>The title.</returns>
    public static string TakeTitle(Generation generation, int index)
    {
        ArgumentNullException.ThrowIfNull(generation);
        var label = generation.Restyle is { } restyle ? $"{restyle.SourceTrackTitle} - {restyle.Style.Name}" : Source(generation).Title;
        return string.Create(CultureInfo.InvariantCulture, $"{label} (take {index})");
    }

    /// <summary>Cuts a text to a length.</summary>
    /// <param name="text">The text.</param>
    /// <param name="maxLength">The limit.</param>
    /// <returns>The text, at most <paramref name="maxLength"/> characters.</returns>
    public static string Cut(string text, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}

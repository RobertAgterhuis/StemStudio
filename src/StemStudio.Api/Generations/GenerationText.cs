using System.Globalization;
using StemStudio.Domain.Generations;

namespace StemStudio.Api.Generations;

/// <summary>Readable names of generations and takes (009 data-model 3.6, 010 FR-011 title rules).</summary>
public static class GenerationText
{
    /// <summary>What a generation was made from: the title snapshot and a short description.</summary>
    /// <param name="generation">The generation.</param>
    /// <returns>Title and description.</returns>
    public static (string Title, string Description) Source(Generation generation)
    {
        ArgumentNullException.ThrowIfNull(generation);
        return generation switch
        {
            { Restyle: { } restyle } => (restyle.SourceTrackTitle, $"restyle, {restyle.Style.Name}"),
            { Song: { } song } => (song.Lyrics.Title, song.Style is { } style ? $"song, {style.Name}" : "song"),
            { VoiceConversion: { } conversion } => ("Voice conversion", $"voice conversion, {conversion.VoiceProfileName}"),
            _ => ("Generation", generation.Kind.ToString().ToLowerInvariant()),
        };
    }

    /// <summary>"&lt;source&gt; - &lt;style&gt; (take n)" for restyles and styled songs, "&lt;lyrics title&gt; (take n)" otherwise.</summary>
    /// <param name="generation">The generation.</param>
    /// <param name="index">The take number.</param>
    /// <returns>The title.</returns>
    public static string TakeTitle(Generation generation, int index)
    {
        ArgumentNullException.ThrowIfNull(generation);
        var label = generation switch
        {
            { Restyle: { } restyle } => $"{restyle.SourceTrackTitle} - {restyle.Style.Name}",
            { Song.Style: { } style } => $"{generation.Song!.Lyrics.Title} - {style.Name}",
            _ => Source(generation).Title,
        };
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

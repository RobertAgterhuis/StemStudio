using StemStudio.Contracts.Generation;

namespace StemStudio.Web.Generation;

/// <summary>The one-line name of a generation in lists and confirmations.</summary>
public static class GenerationTitles
{
    /// <summary>"Jazz trio: Never Gonna Give You Up" (restyle), the lyrics title (song) or "In the voice My voice" (conversion).</summary>
    /// <param name="generation">The generation.</param>
    /// <returns>The title.</returns>
    public static string Of(GenerationDto generation) => generation switch
    {
        { Restyle: { } restyle } => $"{restyle.Style.Name}: {restyle.SourceTrackTitle}",
        { Song: { } song } => song.Lyrics.Title,
        { VoiceConversion: { } conversion } => $"In the voice {conversion.VoiceProfileName}",
        _ => "Generation",
    };
}

using StemStudio.Contracts.Generation;

namespace StemStudio.Web.Generation;

/// <summary>The one-line name of a generation in lists and confirmations.</summary>
public static class GenerationTitles
{
    /// <summary>For example "Jazz trio: Never Gonna Give You Up" for a restyle.</summary>
    /// <param name="generation">The generation.</param>
    /// <returns>The title.</returns>
    public static string Of(GenerationDto generation) =>
        generation.Restyle is { } restyle ? $"{restyle.Style.Name}: {restyle.SourceTrackTitle}" : "Generation";
}

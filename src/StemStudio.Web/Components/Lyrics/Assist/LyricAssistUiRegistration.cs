namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>
/// Composition hook of the 023 UI (lyric assistant pane). Skeleton (architect, 022+023 round start): the frontend-023
/// lane fills it so no other lane edits <c>Program.cs</c>.
/// </summary>
public static class LyricAssistUiRegistration
{
    /// <summary>Registers the 023 UI services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddLyricAssistUi(this IServiceCollection services) => services;
}

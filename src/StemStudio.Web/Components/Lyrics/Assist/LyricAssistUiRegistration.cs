namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>
/// Composition hook of the 023 UI (lyric assistant pane): registers the browser helpers of the lyrics editor page. The
/// session itself is created per page (it belongs to one open sheet).
/// </summary>
public static class LyricAssistUiRegistration
{
    /// <summary>Registers the 023 UI services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddLyricAssistUi(this IServiceCollection services) =>
        services.AddScoped<ILyricEditorInterop, LyricEditorInterop>();
}

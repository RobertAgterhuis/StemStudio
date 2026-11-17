using Microsoft.AspNetCore.WebUtilities;
using StemStudio.Contracts.Agent;

namespace StemStudio.Web.Agent;

/// <summary>
/// Derives the page context of a command (FR-004) from the current address: the page name of
/// <c>PageContext.page</c> (backend API 1.10.0), the open track, the mix loaded in the mixer (<c>?mixId=</c>) and the sheet
/// of the lyrics editor (<c>/lyrics/{id}</c>, <c>PageContext.lyricsId</c>, 1.23.0). A library folder (<c>/library/{folderId}</c>,
/// 1.22.0) is the <c>library</c> context.
/// </summary>
public static class AgentPageContexts
{
    private static readonly HashSet<string> ListPages = new(StringComparer.Ordinal)
    {
        "tracks", "mixes", "library", "styles", "generations", "lyrics", "songs", "voices", "settings",
    };

    /// <summary>Builds the context of an address.</summary>
    /// <param name="uri">The absolute address.</param>
    /// <param name="baseUri">The app's base address.</param>
    /// <returns>The page context.</returns>
    public static PageContextDto From(Uri uri, Uri baseUri)
    {
        var relative = uri.AbsoluteUri.Length > baseUri.AbsoluteUri.Length ? uri.AbsoluteUri[baseUri.AbsoluteUri.Length..] : string.Empty;
        var parts = relative.Split('?', 2);
        var segments = parts[0].Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments switch
        {
            ["tracks", var id, "mixer"] when Guid.TryParse(id, out var trackId) => Mixer(trackId, parts.Length > 1 ? parts[1] : string.Empty),
            ["tracks", var id] when Guid.TryParse(id, out var trackId) => new PageContextDto { Page = "track", TrackId = trackId },
            ["lyrics", var id] when Guid.TryParse(id, out var lyricsId) => new PageContextDto { Page = "lyrics", LyricsId = lyricsId },
            [var first, ..] when ListPages.Contains(first) => new PageContextDto { Page = first },
            _ => new PageContextDto { Page = "import" },
        };
    }

    private static PageContextDto Mixer(Guid trackId, string query)
    {
        var mix = QueryHelpers.ParseQuery(query).TryGetValue("mixId", out var value) && Guid.TryParse(value, out var mixId) ? mixId : (Guid?)null;
        return new PageContextDto { Page = "mixer", TrackId = trackId, MixId = mix };
    }
}

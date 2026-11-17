using System.Text;

namespace StemStudio.Api;

/// <summary>Writes server-sent event text (011 runs, 023 assists).</summary>
public static class SseText
{
    /// <summary>The keep-alive comment.</summary>
    public const string KeepAlive = ": keep-alive\n\n";

    /// <summary>Writes UTF-8 text to the response body and flushes it, so the client sees it at once.</summary>
    /// <param name="response">The response.</param>
    /// <param name="text">The event text (already framed).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task that completes after the flush.</returns>
    public static async Task WriteAsync(HttpResponse response, string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        await response.Body.WriteAsync(Encoding.UTF8.GetBytes(text), cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}

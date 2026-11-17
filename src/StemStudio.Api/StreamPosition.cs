using System.Globalization;

namespace StemStudio.Api;

/// <summary>The client's position in an event stream (011 runs, 023 assists): the <c>after</c> query value or the <c>Last-Event-ID</c> header.</summary>
public static class StreamPosition
{
    /// <summary>Reads the position; the <c>after</c> query value wins over the <c>Last-Event-ID</c> header.</summary>
    /// <param name="request">The request.</param>
    /// <param name="after">The last sequence number the client has; 0 when neither is given.</param>
    /// <returns>False when the given value is not a whole number of 0 or more.</returns>
    public static bool TryParse(HttpRequest request, out int after)
    {
        ArgumentNullException.ThrowIfNull(request);
        var text = request.Query.TryGetValue("after", out var query) ? query.ToString() : request.Headers["Last-Event-ID"].ToString();
        after = 0;
        return text.Length == 0 || int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out after);
    }
}

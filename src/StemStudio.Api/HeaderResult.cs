namespace StemStudio.Api;

/// <summary>Sets one response header, then executes the inner result (e.g. <c>Cache-Control</c> or <c>Content-Disposition</c> of a file).</summary>
/// <param name="Inner">The result.</param>
/// <param name="Name">Header name.</param>
/// <param name="Value">Header value.</param>
public sealed record HeaderResult(IResult Inner, string Name, string Value) : IResult
{
    /// <inheritdoc />
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        httpContext.Response.Headers[Name] = Value;
        return Inner.ExecuteAsync(httpContext);
    }
}

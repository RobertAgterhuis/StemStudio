using System.Net;

namespace StemStudio.Web.Api;

/// <summary>
/// A failed backend call, mapped from an RFC 9457 problem details response
/// (<c>application/problem+json</c>) or from a transport failure.
/// </summary>
/// <param name="Status">HTTP status code (503 when the API could not be reached).</param>
/// <param name="Title">Problem title.</param>
/// <param name="Detail">Problem detail, when present.</param>
/// <param name="FieldErrors">Validation errors per field (empty for non-validation problems).</param>
public sealed record ApiError(
    HttpStatusCode Status,
    string Title,
    string? Detail,
    IReadOnlyDictionary<string, string[]> FieldErrors)
{
    /// <summary>The error used when the API itself cannot be reached.</summary>
    public static ApiError Unreachable { get; } = new(
        HttpStatusCode.ServiceUnavailable,
        "The StemStudio API cannot be reached. Is it running?",
        null,
        new Dictionary<string, string[]>());

    /// <summary>True for <c>409 Conflict</c> (mix name taken, track not retryable).</summary>
    public bool IsConflict => Status == HttpStatusCode.Conflict;

    /// <summary>True for <c>503 Service Unavailable</c> (worker or API unavailable).</summary>
    public bool IsUnavailable => Status == HttpStatusCode.ServiceUnavailable;

    /// <summary>True for <c>404 Not Found</c>.</summary>
    public bool IsNotFound => Status == HttpStatusCode.NotFound;

    /// <summary>A message the user can act on: field errors first, then detail, then title (SC-007).</summary>
    public string UserMessage => FieldErrors.Count > 0
        ? string.Join(" ", FieldErrors.Values.SelectMany(messages => messages))
        : Detail ?? Title;

    /// <summary>Creates an error for a response without a readable problem body.</summary>
    /// <param name="status">The HTTP status code.</param>
    /// <returns>An error whose message names the status code.</returns>
    public static ApiError FromStatus(HttpStatusCode status) =>
        new(status, $"The server returned an error ({(int)status}).", null, new Dictionary<string, string[]>());
}

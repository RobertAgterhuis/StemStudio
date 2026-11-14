using StemStudio.Domain;

namespace StemStudio.Api;

/// <summary>RFC 9457 problem responses used by the endpoints (titles and texts from backend-api.openapi.yaml).</summary>
public static class ApiProblems
{
    /// <summary>400 validation problem with one field error.</summary>
    /// <param name="field">JSON path of the field, e.g. <c>url</c>.</param>
    /// <param name="message">Actionable message.</param>
    /// <returns>The result.</returns>
    public static IResult Validation(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    /// <summary>400 validation problem from domain errors.</summary>
    /// <param name="errors">Domain errors.</param>
    /// <param name="prefix">Path prefix of the request body (e.g. <c>settings</c>), or empty.</param>
    /// <returns>The result.</returns>
    public static IResult Validation(IEnumerable<DomainError> errors, string prefix) =>
        TypedResults.ValidationProblem(errors
            .GroupBy(error => Path(prefix, error.Field), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray(), StringComparer.Ordinal));

    /// <summary>404 problem.</summary>
    /// <param name="detail">What was not found.</param>
    /// <returns>The result.</returns>
    public static IResult NotFound(string detail) => TypedResults.Problem(detail, statusCode: StatusCodes.Status404NotFound, title: "Not found");

    /// <summary>409 problem.</summary>
    /// <param name="title">Short title.</param>
    /// <param name="detail">Explanation.</param>
    /// <returns>The result.</returns>
    public static IResult Conflict(string title, string detail) => TypedResults.Problem(detail, statusCode: StatusCodes.Status409Conflict, title: title);

    /// <summary>413 problem for uploads over 200 MiB.</summary>
    /// <returns>The result.</returns>
    public static IResult TooLarge() => TypedResults.Problem(
        "The file is larger than 200 MiB (209,715,200 bytes).", statusCode: StatusCodes.Status413PayloadTooLarge, title: "File too large");

    /// <summary>415 problem for files that are not MP3, WAV or FLAC.</summary>
    /// <returns>The result.</returns>
    public static IResult UnsupportedType() => TypedResults.Problem(
        "Only MP3, WAV and FLAC files are supported.", statusCode: StatusCodes.Status415UnsupportedMediaType, title: "Unsupported file type");

    /// <summary>503 problem when the worker cannot be reached.</summary>
    /// <returns>The result.</returns>
    public static IResult Unavailable() => TypedResults.Problem(
        "The processing service is unavailable. Existing tracks and mixes remain usable.",
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Processing service unavailable");

    private static string Path(string prefix, string field) => (prefix.Length, field.Length) switch
    {
        (0, _) => field,
        (_, 0) => prefix,
        _ => $"{prefix}.{field}",
    };
}

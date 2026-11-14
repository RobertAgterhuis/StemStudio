namespace StemStudio.Api;

/// <summary>Makes framework-generated problems match the contract (every 400 carries an <c>errors</c> map).</summary>
public static class ProblemDetailsCustomizer
{
    /// <summary>Adds an <c>errors</c> map to 400 problems produced by request binding (e.g. malformed JSON).</summary>
    /// <param name="context">The problem details context.</param>
    public static void Customize(ProblemDetailsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var problem = context.ProblemDetails;
        if (problem.Status == StatusCodes.Status400BadRequest && problem is not HttpValidationProblemDetails && !problem.Extensions.ContainsKey("errors"))
        {
            problem.Extensions["errors"] = new Dictionary<string, string[]>
            {
                ["body"] = ["The request body is not valid. Send JSON as described in the API contract."],
            };
        }
    }

    /// <summary>Status code for an unhandled exception: the status of a <see cref="BadHttpRequestException"/> (e.g. 413), else 500.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The status code.</returns>
    public static int StatusCodeFor(Exception exception) =>
        exception is BadHttpRequestException badRequest ? badRequest.StatusCode : StatusCodes.Status500InternalServerError;
}

using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Agent;

/// <summary>Problem responses of the agent API (contracts/DELTA.md section 1; <c>type</c> = <c>https://stemstudio.local/problems/&lt;code&gt;</c>).</summary>
public static class AgentProblems
{
    /// <summary>Problem code of 422 validation errors.</summary>
    public const string ValidationCode = "validation";

    /// <summary>A coded problem.</summary>
    /// <param name="status">HTTP status.</param>
    /// <param name="code">Code from <see cref="AgentProblemTypes"/>.</param>
    /// <param name="title">Short title.</param>
    /// <param name="detail">Readable explanation.</param>
    /// <returns>The result.</returns>
    public static IResult Coded(int status, string code, string title, string detail) =>
        TypedResults.Problem(detail, statusCode: status, title: title, type: AgentProblemTypes.Uri(code));

    /// <summary>409 with a code.</summary>
    /// <param name="code">Code from <see cref="AgentProblemTypes"/>.</param>
    /// <param name="detail">Readable explanation.</param>
    /// <returns>The result.</returns>
    public static IResult Conflict(string code, string detail) => Coded(StatusCodes.Status409Conflict, code, "Conflict", detail);

    /// <summary>409 <c>run-active</c>.</summary>
    /// <returns>The result.</returns>
    public static IResult RunActive() => Coded(
        StatusCodes.Status409Conflict, AgentProblemTypes.RunActive, "A command is already running", "Wait for the current command to finish or cancel it.");

    /// <summary>422 <c>validation</c> with field errors.</summary>
    /// <param name="field">Field path.</param>
    /// <param name="message">Readable message.</param>
    /// <returns>The result.</returns>
    public static IResult Unprocessable(string field, string message) => TypedResults.Problem(new HttpValidationProblemDetails(
        new Dictionary<string, string[]> { [field] = [message] })
    {
        Type = AgentProblemTypes.Uri(ValidationCode),
        Title = "One or more validation errors occurred.",
        Status = StatusCodes.Status422UnprocessableEntity,
    });
}

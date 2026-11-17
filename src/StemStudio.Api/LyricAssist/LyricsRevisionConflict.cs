using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Api.LyricAssist;

/// <summary>A sheet change lost the race against another change of the same revision (the stored revision moved on).</summary>
/// <param name="currentRevision">The stored revision.</param>
public sealed class LyricsRevisionConflictException(int currentRevision) : Exception("The lyrics changed in another request.")
{
    /// <summary>The stored revision.</summary>
    public int CurrentRevision { get; } = currentRevision;
}

/// <summary>Answers <see cref="LyricsRevisionConflictException"/> as <c>409 revision-conflict</c> with <c>currentRevision</c> (code review, 023).</summary>
public sealed class LyricsRevisionConflictHandler : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        if (exception is not LyricsRevisionConflictException conflict)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        var problem = new ProblemDetails
        {
            Type = LyricAssistProblemTypes.Uri(LyricAssistProblemTypes.RevisionConflict),
            Title = LyricAssistProblems.RevisionConflictTitle,
            Status = StatusCodes.Status409Conflict,
            Extensions =
            {
                [LyricAssistProblemMembers.Code] = LyricAssistProblemTypes.RevisionConflict,
                [LyricAssistProblemMembers.CurrentRevision] = conflict.CurrentRevision,
            },
        };
        await httpContext.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }
}

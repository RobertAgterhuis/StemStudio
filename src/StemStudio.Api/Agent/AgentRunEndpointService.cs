using System.Globalization;
using System.Text.Json;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Agent;

/// <summary>Run endpoints of backend 1.10.0: snapshot, event stream, UI results and cancel.</summary>
/// <param name="runs">Run store.</param>
/// <param name="queries">Read models.</param>
/// <param name="sse">Event stream writer.</param>
/// <param name="lifecycle">Run end bookkeeping.</param>
public sealed class AgentRunEndpointService(IRunStore runs, AgentQueries queries, SseWriter sse, AgentRunLifecycle lifecycle)
{
    /// <summary>The run snapshot.</summary>
    /// <param name="runId">Run id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> GetAsync(Guid runId, CancellationToken cancellationToken) =>
        await SnapshotAsync(runId, cancellationToken) is { } run ? TypedResults.Ok(run) : RunNotFound();

    /// <summary>Streams the events of a run.</summary>
    /// <param name="context">The HTTP context (query <c>after</c>, header <c>Last-Event-ID</c>).</param>
    /// <param name="runId">Run id.</param>
    /// <returns>400, 404, or an empty result after the stream was written.</returns>
    public async Task<IResult> StreamAsync(HttpContext context, Guid runId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!TryPosition(context.Request, out var after))
        {
            return ApiProblems.Validation("after", "after and Last-Event-ID must be whole numbers of 0 or more.");
        }

        if (await runs.GetRunAsync(runId, context.RequestAborted) is null)
        {
            return RunNotFound();
        }

        await sse.StreamAsync(context.Response, runId, after, context.RequestAborted);
        return Results.Empty;
    }

    /// <summary>Delivers the page's answer to a UI tool call.</summary>
    /// <param name="runId">Run id.</param>
    /// <param name="stepId">Step id.</param>
    /// <param name="request">The answer.</param>
    /// <param name="gateway">UI tool gateway of the agent library.</param>
    /// <returns>204, 400, 404 or 409.</returns>
    public async Task<IResult> SubmitUiResultAsync(Guid runId, Guid stepId, SubmitUiResultRequest? request, IUiToolGateway gateway)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        if (Invalid(request) is { } invalid)
        {
            return invalid;
        }

        if (await runs.GetRunAsync(runId, CancellationToken.None) is null)
        {
            return RunNotFound();
        }

        var result = new UiToolResult { Ok = request!.Ok, Result = request.Result?.GetRawText(), Error = request.Error };
        return gateway.TryComplete(runId, stepId, result)
            ? TypedResults.NoContent()
            : AgentProblems.Conflict(AgentProblemTypes.UiResultNotExpected, "This step does not expect a result any more (already answered or timed out).");
    }

    /// <summary>Cancels a run.</summary>
    /// <param name="runId">Run id.</param>
    /// <param name="request">Whether to cancel the jobs it started.</param>
    /// <param name="agent">Run service of the agent library.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404 or 409.</returns>
    public async Task<IResult> CancelAsync(Guid runId, CancelAgentRunRequest? request, IAgentRunService agent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        if (await runs.GetRunAsync(runId, cancellationToken) is null)
        {
            return RunNotFound();
        }

        try
        {
            await agent.CancelAsync(runId, request?.CancelJobs ?? false, cancellationToken);
        }
        catch (AgentConflictException conflict)
        {
            return AgentProblems.Conflict(conflict.ProblemCode, conflict.Message);
        }

        await lifecycle.ReleaseIfEndedAsync(runId, cancellationToken);
        return TypedResults.Accepted((string?)null, await SnapshotAsync(runId, cancellationToken));
    }

    /// <summary>404 for an unknown run.</summary>
    /// <returns>The result.</returns>
    public static IResult RunNotFound() => ApiProblems.NotFound("The run does not exist.");

    /// <summary>Builds the run snapshot.</summary>
    /// <param name="runId">Run id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The snapshot, or null.</returns>
    public async Task<AgentRunDto?> SnapshotAsync(Guid runId, CancellationToken cancellationToken)
    {
        if (await runs.GetRunAsync(runId, cancellationToken) is not { } run)
        {
            return null;
        }

        var steps = await runs.GetStepsAsync(runId, cancellationToken);
        var pending = new HashSet<Guid>();
        foreach (var step in steps.Where(s => s.Status == AgentStepStatusDto.AwaitingApproval))
        {
            if (await runs.GetApprovalAsync(step.Id, cancellationToken) is { Status: ApprovalStatusDto.Pending })
            {
                pending.Add(step.Id);
            }
        }

        return AgentDtoMapping.ToDto(run, steps, await queries.PendingApprovalAsync(runId, pending, cancellationToken));
    }

    private static IResult? Invalid(SubmitUiResultRequest? request)
    {
        if (request is null)
        {
            return ApiProblems.Validation("body", "Send {ok, result?, error?}.");
        }

        if (request.Error is { Length: > AgentContractLimits.ErrorMaxLength })
        {
            return ApiProblems.Validation("error", "The error has at most 500 characters.");
        }

        return request.Result is { } result && JsonSerializer.SerializeToUtf8Bytes(result).Length > AgentContractLimits.ResultMaxBytes
            ? ApiProblems.Validation("result", "The result has at most 4 KB.")
            : null;
    }

    private static bool TryPosition(HttpRequest request, out int after)
    {
        var text = request.Query.TryGetValue("after", out var query) ? query.ToString() : request.Headers["Last-Event-ID"].ToString();
        after = 0;
        return text.Length == 0 || int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out after);
    }
}

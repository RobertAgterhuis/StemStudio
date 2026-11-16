using Microsoft.AspNetCore.Mvc;
using StemStudio.Agent.Ports;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Agent;

/// <summary>Defaults of the agent endpoints.</summary>
public static class AgentEndpointDefaults
{
    /// <summary>Page context of <c>GET /api/agent/tools</c> without <c>page</c>.</summary>
    public const string Page = "tracks";
}

/// <summary>Maps the <c>/api/agent/*</c> routes of backend 1.10.0 (feature 011, tag <c>agent</c>).</summary>
public static class AgentEndpoints
{
    private const string Tag = "agent";
    private const string ConversationRoute = ApiRoutes.AgentConversations + "/{conversationId:guid}";
    private const string RunRoute = ApiRoutes.Agent + "/runs/{runId:guid}";

    /// <summary>Maps the agent endpoints.</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.AgentStatus, async (AgentStatusProbe probe, CancellationToken ct) => TypedResults.Ok(await probe.GetAsync(ct)))
            .WithName("getAgentStatus").WithTags(Tag).Produces<AgentStatusDto>();
        app.MapGet(ApiRoutes.AgentTools, ListTools).WithName("listAgentTools").WithTags(Tag).Produces<List<AgentToolInfoDto>>().ProducesValidationProblem();
        return app.MapSettingsEndpoints().MapConversationEndpoints().MapRunEndpoints();
    }

    private static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.AgentSettings, (AgentSettingsService service, CancellationToken ct) => service.GetAsync(ct))
            .WithName("getAgentSettings").WithTags(Tag).Produces<AgentSettingsDto>();
        app.MapPut(ApiRoutes.AgentSettings, (UpdateAgentSettingsRequest? request, AgentSettingsService service, CancellationToken ct) => service.UpdateAsync(request, ct))
            .WithName("updateAgentSettings").WithTags(Tag).Produces<AgentSettingsDto>().ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        app.MapPost(ApiRoutes.AgentSettingsTest, (TestAgentBackendRequest? request, AgentSettingsService service, CancellationToken ct) => service.TestAsync(request, ct))
            .WithName("testAgentBackend").WithTags(Tag).Produces<BackendTestResultDto>().ProducesValidationProblem();
        return app;
    }

    private static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.AgentConversations, (string? limit, string? before, AgentConversationService service, CancellationToken ct) => service.ListAsync(limit, before, ct))
            .WithName("listConversations").WithTags(Tag).Produces<List<ConversationSummaryDto>>().ProducesValidationProblem();
        app.MapPost(ApiRoutes.AgentConversations, (CreateConversationRequest? request, AgentConversationService service, CancellationToken ct) => service.CreateAsync(request, ct))
            .WithName("createConversation").WithTags(Tag).Produces<ConversationDto>(StatusCodes.Status201Created).ProducesValidationProblem();
        app.MapGet(ConversationRoute, (Guid conversationId, AgentConversationService service, CancellationToken ct) => service.GetAsync(conversationId, ct))
            .WithName("getConversation").WithTags(Tag).Produces<ConversationDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPost(ConversationRoute + "/messages", (Guid conversationId, SendAgentMessageRequest? request, AgentMessageService service, AgentStatusProbe probe, CancellationToken ct) =>
                SendAsync(new MessageCall(conversationId, request, service, probe), ct))
            .WithName("sendAgentMessage").WithTags(Tag).Produces<AgentMessageAcceptedDto>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return app;
    }

    private static IEndpointRouteBuilder MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(RunRoute, (Guid runId, AgentRunEndpointService service, CancellationToken ct) => service.GetAsync(runId, ct))
            .WithName("getAgentRun").WithTags(Tag).Produces<AgentRunDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapGet(RunRoute + "/events", (Guid runId, HttpContext context, AgentRunEndpointService service) => service.StreamAsync(context, runId))
            .WithName("streamAgentRunEvents").WithTags(Tag).Produces(StatusCodes.Status200OK, contentType: "text/event-stream").ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPost(RunRoute + "/ui-results/{stepId:guid}", (Guid runId, Guid stepId, SubmitUiResultRequest? request, AgentRunEndpointService service, [FromServices] IUiToolGateway gateway) =>
                service.SubmitUiResultAsync(runId, stepId, request, gateway))
            .WithName("submitAgentUiResult").WithTags(Tag).Produces(StatusCodes.Status204NoContent).ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        app.MapPost(RunRoute + "/approvals/{stepId:guid}", (Guid runId, Guid stepId, DecideApprovalRequest? request, AgentApprovalService service, CancellationToken ct) =>
                service.DecideAsync(runId, stepId, request, ct))
            .WithName("decideAgentApproval").WithTags(Tag).Produces<ApprovalStateDto>().ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        app.MapPost(RunRoute + "/cancel", (Guid runId, CancelAgentRunRequest? request, AgentRunEndpointService service, [FromServices] IAgentRunService agent, CancellationToken ct) =>
                service.CancelAsync(runId, request, agent, ct))
            .WithName("cancelAgentRun").WithTags(Tag).Produces<AgentRunDto>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        return app;
    }

    private static async Task<IResult> SendAsync(MessageCall call, CancellationToken cancellationToken) =>
        await call.Probe.UnavailableAsync(cancellationToken) ?? await call.Service.SendAsync(call.ConversationId, call.Request, cancellationToken);

    private static IResult ListTools(string? page, string? trackId, [FromServices] IAgentStatusService status)
    {
        Guid? track = null;
        if (trackId is not null)
        {
            if (!Guid.TryParse(trackId, out var parsed))
            {
                return ApiProblems.Validation("trackId", "trackId must be a UUID.");
            }

            track = parsed;
        }

        var context = new PageContextDto { Page = string.IsNullOrWhiteSpace(page) ? AgentEndpointDefaults.Page : page, TrackId = track };
        return TypedResults.Ok(status.ListTools(context));
    }

    private sealed record MessageCall(Guid ConversationId, SendAgentMessageRequest? Request, AgentMessageService Service, AgentStatusProbe Probe);
}

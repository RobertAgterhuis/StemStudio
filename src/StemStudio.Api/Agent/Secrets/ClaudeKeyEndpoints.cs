using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.HttpLogging;
using StemStudio.Agent.Backends;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Agent.Secrets;

/// <summary>
/// <c>GET</c>/<c>PUT</c>/<c>DELETE /api/agent/settings/claude-key</c> (backend 1.21.0, 011 follow-up claude-key, CK-FR-006..010,
/// CK-FR-015). Write-only: no response, problem or log line contains the key; the body is read by the endpoint itself (at most
/// 1 KiB) and HTTP logging is off for the route. Storing never contacts Anthropic and never changes the selected backend.
/// </summary>
public static class ClaudeKeyEndpoints
{
    /// <summary>Detail of 400 <c>invalid-body</c>.</summary>
    public const string InvalidBodyDetail = "Send a JSON object with the field apiKey.";

    private const string Tag = "agent";

    /// <summary>Maps the key endpoints.</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapClaudeKeyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ApiRoutes.AgentClaudeKey).WithTags(Tag).WithHttpLogging(HttpLoggingFields.None);
        group.MapGet(string.Empty, (IClaudeKeyProvider keys) => TypedResults.Ok(ClaudeKeyStatusMapping.ToDto(keys.Resolve())))
            .WithName("getClaudeKeyStatus").Produces<ClaudeKeyStatusDto>();
        group.MapPut(string.Empty, StoreAsync).LocalOnly()
            .WithName("storeClaudeKey").Produces<ClaudeKeyStatusDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge).ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return app;
    }

    /// <summary>Applies the shared <see cref="LocalOnlyKeyEntryFilter"/> (runs before the handler reads the body).</summary>
    /// <param name="builder">The endpoint.</param>
    /// <returns>The endpoint.</returns>
    public static RouteHandlerBuilder LocalOnly(this RouteHandlerBuilder builder) => builder.AddEndpointFilterFactory((factory, next) =>
    {
        var filter = factory.ApplicationServices.GetRequiredService<LocalOnlyKeyEntryFilter>();
        return invocation => filter.InvokeAsync(invocation, next);
    });

    private static async Task<IResult> StoreAsync(HttpRequest request, ClaudeKeyStoreServices services, CancellationToken cancellationToken)
    {
        var body = await ClaudeKeyRequestReader.ReadAsync(request, cancellationToken);
        if (body.Problem is { } problem)
        {
            return problem;
        }

        var parsed = ClaudeApiKey.Parse(body.ApiKey);
        if (parsed.Key is not { } key)
        {
            return Invalid(parsed.Error!);
        }

        if (!services.Protector.Available)
        {
            return AgentProblems.Coded(StatusCodes.Status503ServiceUnavailable, AgentProblemTypes.KeyStoreUnavailable,
                "Key store not available", ClaudeKeyProvider.StoreUnavailableReason(services.Protector.UnavailableReason));
        }

        await services.Writer.StoreAsync(key, cancellationToken);
        services.Keys.Invalidate();
        return TypedResults.Ok(ClaudeKeyStatusMapping.ToDto(services.Keys.Resolve()));
    }

    private static ProblemHttpResult Invalid(string reason) => TypedResults.Problem(
        InvalidDetail(reason),
        statusCode: StatusCodes.Status422UnprocessableEntity,
        title: "Not a Claude API key",
        type: AgentProblemTypes.Uri(AgentProblemTypes.ClaudeKeyInvalid),
        extensions: new Dictionary<string, object?> { ["reason"] = reason });

    private static string InvalidDetail(string reason) => reason switch
    {
        ClaudeKeyInvalidReasons.Empty => "Enter a Claude API key.",
        ClaudeKeyInvalidReasons.AdminKey => "This is an Admin API key. Enter a normal Claude API key instead.",
        _ => "The key must start with sk-ant- and be 40-256 characters (letters, digits, - and _).",
    };
}

/// <summary>Services of the key write endpoints (keeps the handlers' parameter lists short).</summary>
/// <param name="Protector">Key ring state.</param>
/// <param name="Writer">Write side of the stored key.</param>
/// <param name="Keys">Key provider (invalidated after every write).</param>
public sealed record ClaudeKeyStoreServices(ClaudeKeyProtector Protector, IStoredClaudeKeyWriter Writer, IClaudeKeyProvider Keys);

/// <summary>Maps a <see cref="ClaudeKeyResolution"/> to the contract's <c>ClaudeKeyStatus</c> (never the key).</summary>
public static class ClaudeKeyStatusMapping
{
    /// <summary>The status DTO.</summary>
    /// <param name="resolution">The resolution.</param>
    /// <returns>The DTO.</returns>
    public static ClaudeKeyStatusDto ToDto(ClaudeKeyResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        return new ClaudeKeyStatusDto
        {
            Configured = resolution.Configured,
            Source = resolution.Source,
            State = resolution.State,
            LastFour = resolution.LastFour,
            Stored = resolution.Stored,
            StoredLastFour = resolution.StoredLastFour,
            StoredAt = resolution.StoredAt,
            StoreAvailable = resolution.StoreAvailable,
            Reason = resolution.Reason,
            Fix = resolution.Fix,
        };
    }
}

/// <summary>Outcome of reading the key request body: the raw value, or the problem to answer.</summary>
/// <param name="ApiKey">The <c>apiKey</c> string as sent.</param>
/// <param name="Problem">400 or 413, or null.</param>
public sealed record ClaudeKeyBody(string? ApiKey, IResult? Problem);

/// <summary>
/// Reads <c>{"apiKey": "..."}</c> from the request with a 1 KiB limit (<see cref="AgentContractLimits.ClaudeKeyBodyBytes"/>).
/// Failures answer fixed texts; the body is never quoted.
/// </summary>
public static class ClaudeKeyRequestReader
{
    /// <summary>Reads the body.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The value or the problem.</returns>
    public static async Task<ClaudeKeyBody> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var limit = AgentContractLimits.ClaudeKeyBodyBytes;
        if (request.ContentLength > limit)
        {
            return TooLarge();
        }

        var buffer = new byte[limit + 1];
        var length = await FillAsync(request.Body, buffer, cancellationToken);
        if (length > limit)
        {
            return TooLarge();
        }

        return ApiKeyOf(buffer.AsMemory(0, length)) is { } apiKey ? new ClaudeKeyBody(apiKey, null) : InvalidBody();
    }

    private static async Task<int> FillAsync(Stream body, byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        int read;
        while (total < buffer.Length && (read = await body.ReadAsync(buffer.AsMemory(total), cancellationToken)) > 0)
        {
            total += read;
        }

        return total;
    }

    private static string? ApiKeyOf(ReadOnlyMemory<byte> json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object ? StringMember(document.RootElement) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? StringMember(JsonElement root) => root.EnumerateObject()
        .Where(member => member.Name.Equals("apiKey", StringComparison.OrdinalIgnoreCase) && member.Value.ValueKind == JsonValueKind.String)
        .Select(member => member.Value.GetString())
        .FirstOrDefault();

    private static ClaudeKeyBody TooLarge() =>
        new(null, TypedResults.Problem("The request body is larger than 1 KiB.", statusCode: StatusCodes.Status413PayloadTooLarge, title: "Request body too large"));

    private static ClaudeKeyBody InvalidBody() =>
        new(null, AgentProblems.Coded(StatusCodes.Status400BadRequest, AgentProblemTypes.InvalidBody, "Invalid request body", ClaudeKeyEndpoints.InvalidBodyDetail));
}

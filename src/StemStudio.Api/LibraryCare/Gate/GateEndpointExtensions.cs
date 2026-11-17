using System.Globalization;
using StemStudio.Contracts;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.LibraryCare.Gate;

/// <summary>Attaches the write gate to endpoints and serves maintenance (data-model 5.2).</summary>
public static class GateEndpointExtensions
{
    /// <summary>Refuses the endpoint with <c>409 backup-running</c> while a backup runs.</summary>
    /// <typeparam name="TBuilder">The endpoint convention builder.</typeparam>
    /// <param name="builder">The endpoint.</param>
    /// <returns>The builder.</returns>
    public static TBuilder RequireGateOpen<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
            context.HttpContext.RequestServices.GetRequiredService<LibraryGate>().Mode == LibraryGateMode.BackupRunning
                ? GateProblems.BackupRunning()
                : await next(context));

    /// <summary>Adds the maintenance middleware: counts in-flight <c>/api</c> requests and answers 503 during maintenance.</summary>
    /// <param name="app">The application.</param>
    /// <returns>The application.</returns>
    public static IApplicationBuilder UseLibraryGate(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.Ordinal) || IsAlwaysAllowed(context.Request))
        {
            await next(context);
            return;
        }

        var gate = context.RequestServices.GetRequiredService<LibraryGate>();
        using var request = gate.TrackRequest();
        if (gate.IsMaintenance)
        {
            context.Response.Headers.RetryAfter = LibraryCareLimits.MaintenanceRetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            await GateProblems.Maintenance().ExecuteAsync(context);
            return;
        }

        await next(context);
    });

    /// <summary>True for the routes served during maintenance: the status and a library job.</summary>
    /// <param name="request">The request.</param>
    /// <returns>True when allowed.</returns>
    public static bool IsAlwaysAllowed(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!HttpMethods.IsGet(request.Method))
        {
            return false;
        }

        var path = request.Path.Value ?? string.Empty;
        return path == ApiRoutes.LibraryCareStatus
            || (path.StartsWith(ApiRoutes.LibraryJobs + "/", StringComparison.Ordinal) && path.Count(c => c == '/') == 3);
    }
}

/// <summary>Problem responses of the gate.</summary>
public static class GateProblems
{
    /// <summary>409 <c>backup-running</c>.</summary>
    /// <returns>The result.</returns>
    public static IResult BackupRunning() => Typed(
        StatusCodes.Status409Conflict, LibraryCareProblemTypes.BackupRunning, "A backup is running", "A backup is running; try again when it has finished.");

    /// <summary>503 <c>library-maintenance</c> (the caller sets <c>Retry-After</c>).</summary>
    /// <returns>The result.</returns>
    public static IResult Maintenance() => Typed(
        StatusCodes.Status503ServiceUnavailable,
        LibraryCareProblemTypes.LibraryMaintenance,
        "Library is being restored",
        "StemStudio is switching to the restored library; this takes a few seconds.");

    /// <summary>A typed library-care problem without extension members.</summary>
    /// <param name="status">HTTP status.</param>
    /// <param name="code">Problem code (<see cref="LibraryCareProblemTypes"/>).</param>
    /// <param name="title">Title.</param>
    /// <param name="detail">Detail.</param>
    /// <returns>The result.</returns>
    public static IResult Typed(int status, string code, string title, string detail) => Typed(new ProblemText(status, code, title, detail), null);

    /// <summary>A typed library-care problem.</summary>
    /// <param name="text">Status, code, title and detail.</param>
    /// <param name="extensions">Extension members, or null.</param>
    /// <returns>The result.</returns>
    public static IResult Typed(ProblemText text, IDictionary<string, object?>? extensions)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TypedResults.Problem(text.Detail, statusCode: text.Status, title: text.Title, type: LibraryCareProblemTypes.BaseUri + text.Code, extensions: extensions);
    }
}

/// <summary>Status, code, title and detail of a library-care problem.</summary>
/// <param name="Status">HTTP status.</param>
/// <param name="Code">Problem code (<see cref="LibraryCareProblemTypes"/>).</param>
/// <param name="Title">Title.</param>
/// <param name="Detail">Detail.</param>
public sealed record ProblemText(int Status, string Code, string Title, string Detail);

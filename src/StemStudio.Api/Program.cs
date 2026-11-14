using StemStudio.Contracts;
using StemStudio.Contracts.Health;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapOpenApi();
app.MapGet(ApiRoutes.Health, () => TypedResults.Ok(new HealthResponse("ok")))
    .WithName("GetHealth");

await app.RunAsync();

/// <summary>API entry point. Public so integration tests can host it with <c>WebApplicationFactory</c>.</summary>
public partial class Program;

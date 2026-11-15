using StemStudio.Api;

var builder = WebApplication.CreateBuilder(args);
builder.AddStemStudioApi();

var app = builder.Build();
await app.UseStemStudioApiAsync();
await app.RunAsync();

/// <summary>API entry point. Public so integration tests can host it with <c>WebApplicationFactory</c>.</summary>
public partial class Program;

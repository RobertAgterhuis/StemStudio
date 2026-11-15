namespace StemStudio.Contracts.Health;

/// <summary>Body of <c>GET /health</c>.</summary>
/// <param name="Status">Always <c>"ok"</c> when the service is up.</param>
public sealed record HealthResponse(string Status);

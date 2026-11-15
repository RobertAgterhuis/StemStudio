namespace StemStudio.Contracts.Tracks;

/// <summary>Body of <c>POST /api/tracks/{id}/metadata/lookup</c>: corrected names, or both null to retry (backend contract 1.4.0).</summary>
/// <param name="Title">Corrected title (1..500 characters), or null to keep the current one.</param>
/// <param name="Artist">Corrected artist (1..500 characters), or null to keep the current one.</param>
public sealed record MetadataLookupRequest(string? Title = null, string? Artist = null);

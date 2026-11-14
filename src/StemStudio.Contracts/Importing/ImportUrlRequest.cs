namespace StemStudio.Contracts.Importing;

/// <summary>Body of <c>POST /api/imports</c>.</summary>
/// <param name="Url">A YouTube video URL or a Spotify track URL.</param>
public sealed record ImportUrlRequest(string Url);

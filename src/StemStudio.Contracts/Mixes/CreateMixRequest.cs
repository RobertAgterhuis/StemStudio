namespace StemStudio.Contracts.Mixes;

/// <summary>Body of <c>POST /api/mixes</c> (create; also used for "save as").</summary>
/// <param name="TrackId">Track the mix belongs to.</param>
/// <param name="Settings">Name and mixer state.</param>
public sealed record CreateMixRequest(Guid TrackId, MixSettingsDto Settings);

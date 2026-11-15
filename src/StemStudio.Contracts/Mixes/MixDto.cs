namespace StemStudio.Contracts.Mixes;

/// <summary>A saved mix.</summary>
/// <param name="Id">Mix identifier.</param>
/// <param name="TrackId">Track the mix belongs to.</param>
/// <param name="Settings">Name and mixer state.</param>
/// <param name="CreatedAt">Creation time (UTC).</param>
/// <param name="UpdatedAt">Last modification time (UTC).</param>
public sealed record MixDto(Guid Id, Guid TrackId, MixSettingsDto Settings, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

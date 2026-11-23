using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Handouts;

/// <summary>Source of a handout: exactly one of a track and a setlist (backend-api 1.19.0, feature 020).</summary>
public sealed record HandoutSourceDto
{
    /// <summary>The song; absent for a setlist handout.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? TrackId { get; init; }

    /// <summary>The setlist; absent for a song handout.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? SetlistId { get; init; }

    /// <summary>A song handout.</summary>
    /// <param name="trackId">Track id.</param>
    /// <returns>The source.</returns>
    public static HandoutSourceDto Song(Guid trackId) => new() { TrackId = trackId };

    /// <summary>A setlist handout.</summary>
    /// <param name="setlistId">Setlist id.</param>
    /// <returns>The source.</returns>
    public static HandoutSourceDto Setlist(Guid setlistId) => new() { SetlistId = setlistId };
}

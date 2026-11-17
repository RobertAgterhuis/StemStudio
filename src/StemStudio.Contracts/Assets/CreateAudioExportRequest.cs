using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Assets;

/// <summary>Body of <c>POST /api/assets/audio-exports</c> (backend contract 1.5.0).</summary>
public sealed record CreateAudioExportRequest
{
    /// <summary>A completed track.</summary>
    public required Guid TrackId { get; init; }

    /// <summary>Raw stems or the mix.</summary>
    public required ExportModeDto Mode { get; init; }

    /// <summary>Explicit channel ids (1..56, see <see cref="ContractLimits.ExportChannelIdPattern"/>; `take` from 1.14.0), or null = every audible channel (mode mix only).</summary>
    public IReadOnlyList<string>? Channels { get; init; }

    /// <summary>Current mixer state (mode mix; exactly one of <see cref="Mix"/> and <see cref="MixId"/>).</summary>
    public ExportMixDto? Mix { get; init; }

    /// <summary>A saved mix of the track (mode mix; exactly one of <see cref="Mix"/> and <see cref="MixId"/>).</summary>
    public Guid? MixId { get; init; }

    /// <summary>Part of the track, or null for the whole length.</summary>
    public RangeDto? Range { get; init; }

    /// <summary>Output format.</summary>
    public required ExportFormatDto Format { get; init; }

    /// <summary>Item name (1..120 characters), or null for the default name.</summary>
    public string? Name { get; init; }

    /// <summary>
    /// The take to export (1.14.0): raw mode with <c>channels: ["take"]</c> (default range = the take's span), or the take of
    /// <c>mix.take</c> / the saved mix in mix mode. Null (absent on the wire) = no take.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? TakeId { get; init; }
}

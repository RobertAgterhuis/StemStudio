using System.Text.Json.Serialization;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Contracts.Generation;

/// <summary>A generation (1.8.0; song and voice conversion 1.9.0). The details member named by <see cref="Kind"/> is set.</summary>
public sealed record GenerationDto
{
    /// <summary>Generation id.</summary>
    public required Guid Id { get; init; }

    /// <summary>Kind.</summary>
    public required GenerationKindDto Kind { get; init; }

    /// <summary>Status.</summary>
    public required GenerationStateDto Status { get; init; }

    /// <summary>Progress 0..100.</summary>
    public required int Progress { get; init; }

    /// <summary>Readable stage.</summary>
    public string? Stage { get; init; }

    /// <summary>Restyle details (kind restyle).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RestyleDetailsDto? Restyle { get; init; }

    /// <summary>Song details (kind song, 1.9.0).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SongDetailsDto? Song { get; init; }

    /// <summary>Voice-conversion details (kind voiceConversion, 1.9.0).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public VoiceConversionDetailsDto? VoiceConversion { get; init; }

    /// <summary>Takes; empty until completed.</summary>
    public IReadOnlyList<GenerationTakeDto> Takes { get; init; } = [];

    /// <summary>Readable warnings that did not fail the generation.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Where it ran.</summary>
    public JobDeviceDto? Device { get; init; }

    /// <summary>Set iff the status is failed.</summary>
    public GenerationErrorDto? Error { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last change (UTC).</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Completion time (UTC).</summary>
    public DateTimeOffset? CompletedAt { get; init; }
}

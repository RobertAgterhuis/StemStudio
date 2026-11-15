namespace StemStudio.Api.Worker;

/// <summary>One stem of a job result (contract <c>StemInfo</c>).</summary>
/// <param name="Name">Stem name.</param>
/// <param name="Path">Relative worker URL of the stem.</param>
/// <param name="SizeBytes">File size.</param>
public sealed record WorkerStemInfo(string Name, string Path, long SizeBytes);

/// <summary>Beat grid of a job result (contract <c>BeatGrid</c>).</summary>
public sealed record WorkerBeatGrid
{
    /// <summary>Tempo (0 = detection failed).</summary>
    public required double Bpm { get; init; }

    /// <summary>Beat times in seconds.</summary>
    public required IReadOnlyList<double> Beats { get; init; }

    /// <summary>Downbeat times in seconds.</summary>
    public required IReadOnlyList<double> Downbeats { get; init; }

    /// <summary>Beats per bar.</summary>
    public int BeatsPerBar { get; init; } = 4;

    /// <summary>Confidence 0..1.</summary>
    public required double Confidence { get; init; }

    /// <summary>Whether the pulse is clear.</summary>
    public required bool Reliable { get; init; }
}

/// <summary>Result of a completed job (contract <c>JobResult</c>).</summary>
public sealed record WorkerJobResult
{
    /// <summary>Job ID.</summary>
    public required string JobId { get; init; }

    /// <summary>Source metadata.</summary>
    public required WorkerSourceMetadata Metadata { get; init; }

    /// <summary>Duration in seconds.</summary>
    public required double DurationSeconds { get; init; }

    /// <summary>Model name.</summary>
    public required string Model { get; init; }

    /// <summary>Stems.</summary>
    public required IReadOnlyList<WorkerStemInfo> Stems { get; init; }

    /// <summary>Beat grid.</summary>
    public required WorkerBeatGrid BeatGrid { get; init; }
}

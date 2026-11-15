using System.Text.Json.Serialization;

namespace StemStudio.Api.Worker;

/// <summary>Outcome of one parent stem (contract 1.1.0 <c>ParentOutcomeKind</c>, kebab-case on the wire).</summary>
public enum WorkerParentOutcomeKind
{
    /// <summary>Split into detail stems plus rest.</summary>
    [JsonStringEnumMemberName("split")]
    Split,

    /// <summary>(Almost) silent.</summary>
    [JsonStringEnumMemberName("skipped-silent")]
    SkippedSilent,

    /// <summary>Fewer than two audible detail stems.</summary>
    [JsonStringEnumMemberName("nothing-further")]
    NothingFurther,

    /// <summary>A plausibility check failed.</summary>
    [JsonStringEnumMemberName("not-reliable")]
    NotReliable,

    /// <summary>No detail model for the stem.</summary>
    [JsonStringEnumMemberName("not-supported")]
    NotSupported,
}

/// <summary>Identifies one detail stem file of a detail job.</summary>
/// <param name="DetailJobId">Detail job ID.</param>
/// <param name="Parent">Parent stem name.</param>
/// <param name="Name">Detail stem name.</param>
public sealed record WorkerDetailStemRef(string DetailJobId, string Parent, string Name);

/// <summary>One detail stem of a result (contract 1.1.0 <c>DetailStemInfo</c>).</summary>
/// <param name="Name">Detail stem name.</param>
/// <param name="Path">Relative worker URL of the file.</param>
/// <param name="SizeBytes">File size.</param>
public sealed record WorkerDetailStemInfo(string Name, string Path, long SizeBytes);

/// <summary>Outcome of one parent stem (contract 1.1.0 <c>ParentOutcome</c>; diagnostics metrics are not read).</summary>
/// <param name="Parent">Standard stem name.</param>
/// <param name="Outcome">What happened to the stem.</param>
/// <param name="Stems">Kept detail stems with rest last; empty unless split.</param>
public sealed record WorkerParentOutcome(string Parent, WorkerParentOutcomeKind Outcome, IReadOnlyList<WorkerDetailStemInfo> Stems);

/// <summary>Result of a completed detail job (contract 1.1.0 <c>DetailResult</c>).</summary>
public sealed record WorkerDetailResult
{
    /// <summary>Detail job ID.</summary>
    public required string DetailJobId { get; init; }

    /// <summary>One outcome per standard stem.</summary>
    public required IReadOnlyList<WorkerParentOutcome> Parents { get; init; }
}

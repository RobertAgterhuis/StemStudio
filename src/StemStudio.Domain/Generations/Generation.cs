using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Generations;

/// <summary>
/// A generation: a restyle in 009 (010 adds songs and voice conversions). Mirrors the generator job (009 data-model
/// 2.4 and 3.2); terminal states are completed, failed and cancelled; a retry is a new generation with new seeds.
/// </summary>
public sealed partial class Generation
{
    /// <summary>Longest stage text.</summary>
    public const int MaxStageLength = 200;

    /// <summary>Longest error message.</summary>
    public const int MaxErrorLength = 500;

    /// <summary>Most warnings kept.</summary>
    public const int MaxWarnings = 5;

    /// <summary>Most takes.</summary>
    public const int MaxTakes = 2;

    /// <summary>Error message used when the generator gives none.</summary>
    public const string FailedDefault = "The generation failed.";

    private List<int> _seeds = [];
    private List<GenerationTake> _takes = [];
    private List<string> _warnings = [];

    private Generation()
    {
    }

    /// <summary>Identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Kind.</summary>
    public GenerationKind Kind { get; private set; }

    /// <summary>Status.</summary>
    public GenerationState Status { get; private set; }

    /// <summary>Progress 0-100.</summary>
    public int Progress { get; private set; }

    /// <summary>Readable stage.</summary>
    public string? Stage { get; private set; }

    /// <summary>Restyle details (kind restyle).</summary>
    public RestyleDetails? Restyle { get; private set; }

    /// <summary>The source track of a restyle (copied for queries); null after it was deleted.</summary>
    public Guid? SourceTrackId { get; private set; }

    /// <summary>One seed per take.</summary>
    public IReadOnlyList<int> Seeds => _seeds;

    /// <summary>The generator job once submitted (32 lower-case hex characters).</summary>
    public string? GeneratorJobId { get; private set; }

    /// <summary>The generator's device once known.</summary>
    public JobDevice? Device { get; private set; }

    /// <summary>Set exactly when failed.</summary>
    public GenerationError? Error { get; private set; }

    /// <summary>Takes; empty until completed.</summary>
    public IReadOnlyList<GenerationTake> Takes => _takes;

    /// <summary>Warnings (at most 5).</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>Creation time (UTC).</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Last change (UTC).</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Completion time (UTC).</summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>True until completed, failed or cancelled.</summary>
    public bool IsActive => !IsTerminal;

    /// <summary>True when completed, failed or cancelled.</summary>
    public bool IsTerminal => Status is GenerationState.Completed or GenerationState.Failed or GenerationState.Cancelled;

    /// <summary>True when failed or cancelled.</summary>
    public bool CanRetry => Status is GenerationState.Failed or GenerationState.Cancelled;

    /// <summary>The library items of the takes.</summary>
    public IReadOnlyList<Guid> LibraryAssetIds => _takes.Where(take => take.LibraryAssetId is not null).Select(take => take.LibraryAssetId!.Value).ToList();

    /// <summary>Creates a queued restyle.</summary>
    /// <param name="id">New identifier.</param>
    /// <param name="details">What it is made from.</param>
    /// <param name="seeds">One seed per take (1-2, each &gt;= 0).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The generation.</returns>
    public static Generation CreateRestyle(Guid id, RestyleDetails details, IReadOnlyList<int> seeds, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(details);
        var generation = New(id, GenerationKind.Restyle, seeds, nowUtc);
        (generation.Restyle, generation.SourceTrackId) = (details, details.SourceTrackId);
        return generation;
    }

    /// <summary>A new queued generation with the same parameters and new seeds.</summary>
    /// <param name="id">New identifier.</param>
    /// <param name="seeds">New seeds (same count).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The new generation.</returns>
    /// <exception cref="InvalidOperationException">Not failed or cancelled.</exception>
    public Generation Retry(Guid id, IReadOnlyList<int> seeds, DateTime nowUtc)
    {
        if (!CanRetry)
        {
            throw new InvalidOperationException("Only failed or cancelled generations can be retried.");
        }

        var copy = New(id, Kind, seeds, nowUtc);
        CopyDetailsTo(copy);
        return copy;
    }

    private static Generation New(Guid id, GenerationKind kind, IReadOnlyList<int> seeds, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(seeds);
        if (seeds.Count is < 1 or > MaxTakes || seeds.Any(seed => seed < 0))
        {
            throw new ArgumentException("A generation needs one or two seeds of at least 0.", nameof(seeds));
        }

        return new Generation { Id = id, Kind = kind, Status = GenerationState.Queued, Stage = "Queued", _seeds = seeds.ToList(), CreatedAt = nowUtc, UpdatedAt = nowUtc };
    }

    private void CopyDetailsTo(Generation copy) => (copy.Restyle, copy.SourceTrackId) = (Restyle, SourceTrackId);
}

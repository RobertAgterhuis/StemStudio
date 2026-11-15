using StemStudio.Contracts.Styles;

namespace StemStudio.Contracts.Generation;

/// <summary>What a restyle was made from (1.8.0); snapshots survive deletion of the track or style.</summary>
public sealed record RestyleDetailsDto
{
    /// <summary>The source track; null after it was deleted.</summary>
    public Guid? SourceTrackId { get; init; }

    /// <summary>Title snapshot of the source track.</summary>
    public required string SourceTrackTitle { get; init; }

    /// <summary>The range.</summary>
    public required SourceRangeDto Range { get; init; }

    /// <summary>Stems that were summed.</summary>
    public IReadOnlyList<string> IncludedStems { get; init; } = [];

    /// <summary>The style snapshot.</summary>
    public required StyleSnapshotDto Style { get; init; }

    /// <summary>The strength used.</summary>
    public required StrengthDto Strength { get; init; }

    /// <summary>The caption sent to the model.</summary>
    public required string Caption { get; init; }
}

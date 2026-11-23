namespace StemStudio.Contracts.InstrumentViews;

/// <summary>Body of <c>PUT .../chords/segments/{segmentIndex}</c> (backend-api 1.15.0, feature 016).</summary>
public sealed record EditChordRequest
{
    /// <summary>Harte label matching <see cref="ContractLimits.HarteLabelPattern"/>.</summary>
    public required string Label { get; init; }
}

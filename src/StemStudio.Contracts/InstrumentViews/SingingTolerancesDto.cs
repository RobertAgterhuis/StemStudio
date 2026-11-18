namespace StemStudio.Contracts.InstrumentViews;

/// <summary>Singing tolerances in cents per level (backend-api 1.15.0, feature 016, data-model 3.4).</summary>
public sealed record SingingTolerancesDto
{
    /// <summary>Beginner tolerance.</summary>
    public int Beginner { get; init; } = 100;

    /// <summary>Advanced tolerance.</summary>
    public int Advanced { get; init; } = 50;

    /// <summary>Expert tolerance.</summary>
    public int Expert { get; init; } = 25;

    /// <summary>Lowest adjustable value.</summary>
    public int Min { get; init; } = ContractLimits.MinToleranceCents;

    /// <summary>Highest adjustable value.</summary>
    public int Max { get; init; } = ContractLimits.MaxToleranceCents;

    /// <summary>Step of the slider.</summary>
    public int Step { get; init; } = ContractLimits.ToleranceStepCents;
}

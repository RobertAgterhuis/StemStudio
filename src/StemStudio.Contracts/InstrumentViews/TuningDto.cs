namespace StemStudio.Contracts.InstrumentViews;

/// <summary>One tuning of the catalogue (backend-api 1.15.0, feature 016, data-model 4.1).</summary>
public sealed record TuningDto
{
    /// <summary>Tuning id.</summary>
    public required TuningIdDto Id { get; init; }

    /// <summary><c>guitar</c> or <c>bass</c>.</summary>
    public required string Instrument { get; init; }

    /// <summary>Display label.</summary>
    public required string Label { get; init; }

    /// <summary>Sounding MIDI pitches, lowest string first.</summary>
    public required IReadOnlyList<int> Pitches { get; init; }
}

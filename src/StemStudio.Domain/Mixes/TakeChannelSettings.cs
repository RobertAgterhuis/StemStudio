namespace StemStudio.Domain.Mixes;

/// <summary>The take channel strip of a mix (feature 015, data-model 2.6): one take with its fader, pan, mute and solo.</summary>
public sealed record TakeChannelSettings : FaderSettings
{
    /// <summary>Creates the settings with the take defaults (volume 1, centre).</summary>
    public TakeChannelSettings()
    {
        Volume = 1.0;
    }

    /// <summary>The take shown in the strip.</summary>
    public required Guid TakeId { get; init; }

    /// <summary>Writes the violations of the value ranges into a collector.</summary>
    /// <param name="errors">Collector positioned at the take.</param>
    internal void Validate(ValidationErrors errors) => ValidateRanges(errors);
}

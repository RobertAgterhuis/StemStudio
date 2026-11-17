namespace StemStudio.Contracts.Recording;

/// <summary>The take channel strip of a mix (data-model 2.6) (backend-api 1.14.0, feature 015).</summary>
public sealed record TakeChannelSettingsDto
{
    /// <summary>The take shown in the strip.</summary>
    public required Guid TakeId { get; init; }

    /// <summary>Fader 0..1.</summary>
    public required double Volume { get; init; }

    /// <summary>Pan -1..1.</summary>
    public required double Pan { get; init; }

    /// <summary>Muted.</summary>
    public required bool Mute { get; init; }

    /// <summary>Soloed.</summary>
    public required bool Solo { get; init; }
}

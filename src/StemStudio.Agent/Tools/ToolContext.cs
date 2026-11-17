using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Tools;

/// <summary>State of a backing service (generation or voice) as seen by the registry.</summary>
/// <param name="Ready">True when the tools that need it may be offered.</param>
/// <param name="Reason">Readable reason when not ready.</param>
public sealed record ServiceAvailability(bool Ready, string? Reason)
{
    /// <summary>Reason used while the state is not known yet.</summary>
    public const string UnknownReason = "The service status is not known yet.";

    /// <summary>A ready service.</summary>
    public static ServiceAvailability Available { get; } = new(true, null);

    /// <summary>A service whose status could not be read.</summary>
    public static ServiceAvailability Unknown { get; } = new(false, UnknownReason);

    /// <summary>A service that is not ready.</summary>
    /// <param name="reason">Readable reason.</param>
    /// <returns>The availability.</returns>
    public static ServiceAvailability Unavailable(string reason) => new(false, reason);
}

/// <summary>What the registry needs to select the tools of one request (page context and service states).</summary>
public sealed record ToolContext
{
    /// <summary>Reason shown for mixer tools while no mixer is open.</summary>
    public const string MixerClosedReason = "No mixer is open; use open_mixer first.";

    /// <summary>Page name, lower case (PageContext.page).</summary>
    public required string Page { get; init; }

    /// <summary>True while a mixer page is open in the session.</summary>
    public required bool MixerOpen { get; init; }

    /// <summary>Generation service state (009/010).</summary>
    public required ServiceAvailability Generation { get; init; }

    /// <summary>Voice service state (010).</summary>
    public required ServiceAvailability Voice { get; init; }

    /// <summary>Model layer of the lyric assistant (023, <c>+service lyricModel</c>); unknown (not offered) until read.</summary>
    public ServiceAvailability LyricModel { get; init; } = ServiceAvailability.Unknown;

    /// <summary>Builds the context of a message's page context.</summary>
    /// <param name="page">Page context sent with the message.</param>
    /// <param name="generation">Generation service state.</param>
    /// <param name="voice">Voice service state.</param>
    /// <returns>The context.</returns>
    public static ToolContext From(PageContextDto page, ServiceAvailability generation, ServiceAvailability voice) =>
        From(page, generation, voice, ServiceAvailability.Unknown);

    /// <summary>Builds the context of a message's page context with the lyric model state (023).</summary>
    /// <param name="page">Page context sent with the message.</param>
    /// <param name="generation">Generation service state.</param>
    /// <param name="voice">Voice service state.</param>
    /// <param name="lyricModel">Lyric model state.</param>
    /// <returns>The context.</returns>
    public static ToolContext From(PageContextDto page, ServiceAvailability generation, ServiceAvailability voice, ServiceAvailability lyricModel)
    {
        ArgumentNullException.ThrowIfNull(page);
        var name = page.Page.Trim().ToLowerInvariant();
        return new ToolContext { Page = name, MixerOpen = name == "mixer", Generation = generation, Voice = voice, LyricModel = lyricModel };
    }
}

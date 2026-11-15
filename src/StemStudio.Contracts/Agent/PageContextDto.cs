namespace StemStudio.Contracts.Agent;

/// <summary>Page the command was typed on; drives the offered tools (mixer UI tools only on a mixer page).</summary>
public sealed record PageContextDto
{
    /// <summary>Page key, at most 40 characters: <c>import</c>, <c>tracks</c>, <c>track</c>, <c>mixer</c>, <c>mixes</c>, <c>library</c>, <c>settings</c>, <c>help</c>.</summary>
    public required string Page { get; init; }

    /// <summary>Track shown on the page, when any.</summary>
    public Guid? TrackId { get; init; }

    /// <summary>Mix loaded in the mixer, when any.</summary>
    public Guid? MixId { get; init; }
}

using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Mixes;

/// <summary>Aggregate root for a saved mixer configuration of one track (FR-016, FR-017).</summary>
public sealed class Mix
{
    private List<ChannelSettings> _channels = [];
    private List<string> _expanded = [];
    private List<DetailChannelSettings> _detailChannels = [];

    private Mix()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
        Metronome = null!;
    }

    /// <summary>Identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Track the mix belongs to.</summary>
    public Guid TrackId { get; private set; }

    /// <summary>Trimmed name.</summary>
    public string Name { get; private set; }

    /// <summary>Comparison form of <see cref="Name"/>; unique per track.</summary>
    public string NormalizedName { get; private set; }

    /// <summary>Master volume 0..1.</summary>
    public double MasterVolume { get; private set; }

    /// <summary>Channel settings, at most one per stem.</summary>
    public IReadOnlyList<ChannelSettings> Channels => _channels;

    /// <summary>Metronome settings.</summary>
    public MetronomeSettings Metronome { get; private set; }

    /// <summary>Loop region, or null.</summary>
    public LoopRegion? Loop { get; private set; }

    /// <summary>Parents shown expanded (feature 002).</summary>
    public IReadOnlyList<string> Expanded => _expanded;

    /// <summary>Detail channel settings, at most one per (parent, detail stem) (feature 002).</summary>
    public IReadOnlyList<DetailChannelSettings> DetailChannels => _detailChannels;

    /// <summary>Creation time (UTC).</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Last modification time (UTC).</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>The current settings.</summary>
    public MixSettings Settings => new()
    {
        Name = Name,
        MasterVolume = MasterVolume,
        Channels = Channels,
        Metronome = Metronome,
        Loop = Loop,
        Expanded = Expanded,
        DetailChannels = DetailChannels,
    };

    /// <summary>Creates a mix after validating the settings against the track.</summary>
    /// <param name="id">New identifier.</param>
    /// <param name="track">The completed track.</param>
    /// <param name="settings">Name and mixer state.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The mix.</returns>
    /// <exception cref="DomainValidationException">The settings violate a rule.</exception>
    public static Mix Create(Guid id, Track track, MixSettings settings, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(track);
        var mix = new Mix { Id = id, TrackId = track.Id, CreatedAt = nowUtc };
        mix.Update(track, settings, nowUtc);
        return mix;
    }

    /// <summary>Replaces name and settings (validated against the track) and sets <see cref="UpdatedAt"/>.</summary>
    /// <param name="track">The mix's track.</param>
    /// <param name="settings">New name and mixer state.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <exception cref="DomainValidationException">The settings violate a rule; the mix is unchanged.</exception>
    public void Update(Track track, MixSettings settings, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(settings);
        if (track.Id != TrackId)
        {
            throw new ArgumentException("The track is not the mix's track.", nameof(track));
        }

        var errors = settings.Validate(track);
        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        Apply(settings, nowUtc);
    }

    /// <summary>Creates a copy named "&lt;name&gt; (copy)", "(copy 2)", ... (see <see cref="MixNaming.CopyName"/>).</summary>
    /// <param name="id">Identifier of the copy.</param>
    /// <param name="takenNames">Names already used for the track.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The copy.</returns>
    public Mix Duplicate(Guid id, IEnumerable<string> takenNames, DateTime nowUtc)
    {
        var copy = new Mix { Id = id, TrackId = TrackId, CreatedAt = nowUtc };
        copy.Apply(Settings with { Name = MixNaming.CopyName(Name, takenNames) }, nowUtc);
        return copy;
    }

    private void Apply(MixSettings settings, DateTime nowUtc)
    {
        (Name, NormalizedName) = (settings.Name.Trim(), MixNaming.Normalize(settings.Name));
        (MasterVolume, _channels, Metronome, Loop) = (settings.MasterVolume, settings.Channels.ToList(), settings.Metronome, settings.Loop);
        (_expanded, _detailChannels) = (settings.Expanded.ToList(), settings.DetailChannels.ToList());
        UpdatedAt = nowUtc;
    }
}

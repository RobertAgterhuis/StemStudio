using StemStudio.Contracts.Mixes;

namespace StemStudio.Web.Mixer;

/// <summary>Mixer state of one detail stem of a split parent (FR-012); its channel id is <c>&lt;parent&gt;.&lt;name&gt;</c>.</summary>
/// <param name="parent">The parent stem, e.g. <c>drums</c>.</param>
/// <param name="name">The detail stem name, e.g. <c>snare</c>.</param>
public sealed class DetailChannelState(string parent, string name)
{
    /// <summary>The parent stem.</summary>
    public string Parent { get; } = parent;

    /// <summary>The detail stem name.</summary>
    public string Name { get; } = name;

    /// <summary>The channel strip state; <see cref="ChannelState.Stem"/> is the engine channel id.</summary>
    public ChannelState Channel { get; } = new($"{parent}.{name}");

    /// <summary>The engine channel id, e.g. <c>drums.snare</c>.</summary>
    public string Id => Channel.Stem;

    /// <summary>The settings as saved in a mix.</summary>
    /// <returns>The contract DTO.</returns>
    public DetailChannelSettingsDto ToDto() => new(Parent, Name, Channel.Volume, Channel.Pan, Channel.Mute, Channel.Solo);

    /// <summary>Copies saved settings, or restores defaults.</summary>
    /// <param name="settings">The saved entry, or null for defaults.</param>
    public void Apply(DetailChannelSettingsDto? settings) =>
        Channel.Apply(settings is null ? null : new ChannelSettingsDto(Id, settings.Volume, settings.Pan, settings.Mute, settings.Solo));
}

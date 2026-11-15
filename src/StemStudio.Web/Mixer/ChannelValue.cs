namespace StemStudio.Web.Mixer;

/// <summary>A numeric control change of one channel (fader gain 0..1 or pan -1..1).</summary>
/// <param name="Id">The channel id (stem name or <c>&lt;parent&gt;.&lt;detail&gt;</c>).</param>
/// <param name="Value">The new value.</param>
public sealed record ChannelValue(string Id, double Value);

using System.Globalization;
using StemStudio.Domain.Recording;

namespace StemStudio.Domain.Rendering;

/// <summary>The take of an export (feature 015, data-model 2.7): its padded <c>audio.flac</c> and its span.</summary>
/// <param name="AssetId">The take's library item.</param>
/// <param name="Name">The take name (the contents text starts with it).</param>
/// <param name="StartSample">First song sample.</param>
/// <param name="LengthSamples">Length in samples.</param>
public sealed record RenderTake(Guid AssetId, string Name, long StartSample, long LengthSamples)
{
    /// <summary>The take's file relative to the API data directory.</summary>
    public string Path => string.Create(CultureInfo.InvariantCulture, $"assets/{AssetId:D}/audio.flac");

    /// <summary>The take's span in seconds.</summary>
    public ExportRange Span => new(LatencyConversion.ToSeconds(StartSample), LatencyConversion.ToSeconds(StartSample + LengthSamples));

    /// <summary>The range of a raw take export: the span, or a requested range intersected with it (null when they do not overlap).</summary>
    /// <param name="requested">The requested range, or null.</param>
    /// <returns>The range or null.</returns>
    public ExportRange? RawRange(ExportRange? requested)
    {
        var span = Span;
        if (requested is null)
        {
            return span;
        }

        var (start, end) = (Math.Max(span.StartSeconds, requested.StartSeconds), Math.Min(span.EndSeconds, requested.EndSeconds));
        return end > start ? new ExportRange(start, end) : null;
    }
}

/// <summary>The take channel of a render plan (feature 015): the gate rule of the take strip and the contents text.</summary>
internal static class TakePlan
{
    /// <summary>The planned take input, or null when the take is not in the export.</summary>
    /// <param name="source">The track's channels with the take.</param>
    /// <param name="mode">Raw or mixed.</param>
    /// <param name="channels">Explicit channel ids, or null.</param>
    /// <param name="mix">The mix (mix mode).</param>
    /// <param name="anySolo">True when a visible channel or the take is soloed.</param>
    /// <returns>The input or null.</returns>
    /// <exception cref="TakeRequiredException">The channel <c>take</c> is listed without a take.</exception>
    public static PlannedInput? Input(RenderSource source, ExportMode mode, IReadOnlyList<string>? channels, ExportMix? mix, bool anySolo)
    {
        var listed = channels?.Contains(RenderPlanner.TakeChannelId, StringComparer.Ordinal) == true;
        var strip = mix?.Take;
        if (listed && (source.Take is null || (mode == ExportMode.Mix && strip is null)))
        {
            throw new TakeRequiredException();
        }

        var (gain, pan) = mode == ExportMode.Raw ? Raw(listed) : Mixed(listed, channels is null, strip, anySolo);
        return source.Take is { } take && gain > 0 ? new PlannedInput(RenderPlanner.TakeChannelId, take.Path, gain, pan) : null;
    }

    /// <summary>"Take n - &lt;channels&gt;" or "Take n" when the take is in the plan, else the channel text.</summary>
    /// <param name="channelText">The text of the stem channels (empty when none).</param>
    /// <param name="take">The take in the plan, or null.</param>
    /// <returns>The text.</returns>
    public static string Text(string channelText, RenderTake? take) => take switch
    {
        null => channelText,
        _ when channelText.Length == 0 => take.Name,
        _ => $"{take.Name} - {channelText}",
    };

    private static (double Gain, double Pan) Raw(bool listed) => listed ? (1, 0) : (0, 0);

    private static (double Gain, double Pan) Mixed(bool listed, bool audibleLevel, Mixes.TakeChannelSettings? strip, bool anySolo) => strip switch
    {
        null => (0, 0),
        _ when listed => (strip.Volume, strip.Pan),
        _ when audibleLevel && !strip.Mute && (!anySolo || strip.Solo) => (strip.Volume, strip.Pan),
        _ => (0, 0),
    };
}

using StemStudio.Contracts.Insights;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Insights;

/// <summary>Builds the tips of every stem from the current track state (data-model 5.3; filled by User Story 2).</summary>
public sealed class QualityTips
{
    /// <summary>Tips per ChannelId (at most two each).</summary>
    /// <param name="track">The track with a report.</param>
    /// <returns>ChannelId -> tips.</returns>
    public IReadOnlyDictionary<string, IReadOnlyList<TipDto>> For(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        return new Dictionary<string, IReadOnlyList<TipDto>>(StringComparer.Ordinal);
    }
}

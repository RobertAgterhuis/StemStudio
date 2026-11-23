#pragma warning disable IDE0005, CS9113, CS1998, IDE0060, CA1822, CA1801, IDE0051, IDE0052, CS0169, CS0414
using StemStudio.Contracts.Insights;
using StemStudio.Domain.Insights;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Insights;

/// <summary>
/// Builds the tips of every stem from the current track state on every read (data-model 5.3, FR-005): a detail split changes
/// the tips without a new analysis. The high-quality re-separation tip (rule 3, with the 003 device estimate) is deferred
/// with gate G2, so no estimate is needed in 1.19.0.
/// </summary>
public sealed class QualityTips
{
    /// <summary>Tips per ChannelId (at most two each).</summary>
    /// <param name="track">The track with a report.</param>
    /// <returns>ChannelId -> tips.</returns>
    public IReadOnlyDictionary<string, IReadOnlyList<TipDto>> For(Track track)
    {
        throw new NotImplementedException();
    }

    private static TipDto ToDto(Tip tip) => new()
    {
        Code = Enum.Parse<TipCodeDto>(tip.Code.ToString()),
        Text = tip.Text,
        Action = tip.Action is { } action
            ? new TipActionDto { Kind = Enum.Parse<TipActionKindDto>(action.Kind.ToString()), Channel = action.Channel, Parent = action.Parent, Child = action.Child }
            : null,
    };
}

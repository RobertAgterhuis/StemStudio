using StemStudio.Contracts.Insights;

namespace StemStudio.Web.Insights;

/// <summary>Button texts of the tip actions (ui-test-ids 1.19.0 quality-tip-action; the G2 re-separation is deferred).</summary>
public static class TipText
{
    /// <summary>"Split further", "Open detail stem" or "Mute".</summary>
    /// <param name="kind">The action kind.</param>
    /// <returns>The button text.</returns>
    public static string Button(TipActionKindDto kind) => kind switch
    {
        TipActionKindDto.SplitDetail => "Split further",
        TipActionKindDto.OpenDetail => "Open detail stem",
        _ => "Mute",
    };
}
